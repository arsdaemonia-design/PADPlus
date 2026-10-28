# Auditoría Técnica y Plan Maestro de Modernización: BetterJoy (.NET 8) — Enfoque Switch a XInput (PC)

## 1. Origen de los Errores: ¿Tu Migración vs. Repositorio Original?

* **Tu migración a .NET 8 (95% limpia y bien ejecutada)**:
  * Convertiste correctamente [BetterJoy.csproj](https://drive.google.com/file/d/1S5rlEq22ioQDXDUOc8vLy2uBdpjyCHk4/view?usp=drivesdk) al formato moderno SDK-style (`net8.0-windows`), mantuviste los recursos `.resx`, actualizaste `Nefarius.ViGEm.Client` y agregaste una protección clave en `Joycon.cs` (`if (ret < 12) return ret;`) para descartar paquetes HID corruptos.
  * De tu lado solo quedaron **detalles cosméticos menores**: dos etiquetas duplicadas (`<Version>` y `<SatelliteResourceLanguages>`) en `.csproj`, el archivo viejo `packages.config` que ya no se usa, y un par de llamadas (`Process.Start` y `Assembly.Location`) cuyo comportamiento por defecto cambió entre .NET Framework 4.6.1 y .NET 8.
* **Del repositorio original de Davidobot (Deuda histórica)**:
  * **Todos los bugs graves de arquitectura y conectividad** (la condición de carrera en `HIDapi` entre la UI y el hilo `Poll`, la saturación Bluetooth por la cola de `Rumble` sin bloqueo, la fuga de eventos `Click +=` al reconectar mandos, el crash en `UpdServer.cs` y el cálculo roto `(middleX - middleX)` de N64) **son errores originales del código de Davidobot**. Son la causa directa de la mayoría de los 585 issues abiertos en el repositorio original.

---

## 2. Reenfoque Estratégico del Proyecto: ¿Emuladores o Traductor Switch $\rightarrow$ XInput?

* **Por qué el enfoque "ForCemu" quedó obsoleto**: Emuladores como Citra y Yuzu cerraron, y los emuladores activos actuales (**Cemu 2.0+**, **Dolphin**, **Ryujinx**) ya integraron **SDL2/SDL3 nativo**, por lo que leen Joy-Cons (incluso combinados) y Pro Controllers con giroscopio directamente sin necesitar servidores UDP (`Cemuhook`) ni herramientas externas.
* **Dónde está la verdadera necesidad hoy en PC**:
  * Plataformas como **PC Game Pass (Xbox App)**, **Epic Games Store**, **GOG**, **EA App**, **Ubisoft Connect** y juegos standalone **exigen XInput (mando de Xbox 360/One)** y no tienen la capa de traducción de Steam Input.
  * Además, Windows por defecto detecta cada Joy-Con Bluetooth como un dispositivo `DirectInput` separado con ejes rotos y sin vibración, provocando **doble entrada (double input)** cuando se intenta emular un mando de Xbox encima.

### El Mejor Camino para Revivir el Proyecto
Reenfocar BetterJoy como un **Traductor Universal Switch-a-XInput "Zero-Touch" (Plug & Play) para Windows**:

1. **Prioridad #1 — Motor XInput (Xbox 360) Impecable (`ShowAsXInput = true`)**:
   * Unir automáticamente Joy-Con Izquierdo + Derecho en un único mando **Xbox 360 virtual vía ViGEmBus**, o usar un Joy-Con individual en horizontal, Pro Controller, SNES/N64/Genesis NSO y mandos clónicos/3rd party.
   * Traducir la vibración estándar de XInput (motores `LargeMotor` / `SmallMotor` de Xbox) a **HD Rumble de Switch** con *rate-limiting* para que nunca cause lag por Bluetooth.
2. **Prioridad #2 — Conexión Bluetooth y USB 100% Automática (Sin abrir Configuración de Windows ni reconectar a mano)**:
   * Eliminar el bucle de dispositivos "fantasma" de Windows, estabilizar el *handshake* Bluetooth y agregar **Auto-Emparejamiento Bluetooth nativo** (ver Sección 3).
3. **Prioridad #3 — Ocultamiento Transparente con `HidHide` (Adiós al "Doble Input")**:
   * En Game Pass y Epic Games, si el juego ve tanto el mando de Switch real (DirectInput) como el mando de Xbox virtual (XInput), los menús saltan dos veces al mover el stick. Integrar **`HidHide`** automáticamente al conectar el mando soluciona esto por completo.
4. **Prioridad #4 — Giroscopio Traducido a Stick Derecho de Xbox o Ratón (*Gyro Aiming* en XInput)**:
   * Como el protocolo XInput de Xbox no tiene canal nativo de giroscopio, conservar y pulir la función que **inyecta el giroscopio del mando de Switch dentro del Stick Derecho del mando Xbox virtual (o del ratón)** permite tener *Gyro Aiming* en cualquier shooter de Game Pass o PC que solo soporte mando de Xbox.
5. **¿Qué hacer con `UpdServer.cs` (Cemuhook UDP) y `ShowAsDS4`?**:
   * **Desactivar `MotionServer` por defecto** (`MotionServer = false` en `App.config`) para no abrir sockets UDP ni gastar CPU en cada paquete de 5 ms cuando el usuario juega en PC.

---

## 3. ¿Por Qué Fallaba el Reconocimiento Automático y el Bluetooth en el BetterJoy Antiguo? (Y Cómo Resolverlo)

En el código actual de [Program.cs](https://drive.google.com/file/d/1znkBIcKGWRPldh3dr9eS46hR7Sg6YBls/view?usp=drivesdk) y [Joycon.cs](https://drive.google.com/file/d/16QWZt8Ig89McFxFoRVfKi4uBvqI5uLBX/view?usp=drivesdk) existen **4 bugs específicos** que explican por qué antes había que abrir cosas manualmente y por qué Bluetooth fallaba tanto:

### Causa 1: El bucle infinito de dispositivos Bluetooth "Fantasma" en Windows
* **Qué pasa hoy (`Program.cs`, L116–328 y `Joycon.cs`, L884–890)**: Cuando emparejas un mando de Switch por Bluetooth en Windows y luego el mando se apaga, **Windows mantiene el nodo HID registrado en el sistema**. `hid_enumerate()` lo sigue devolviendo aunque el mando esté apagado en tu mesa. BetterJoy abre el `handle`, intenta inicializarlo (`Attach()`), se queda colgado varios segundos leyendo memoria SPI sin respuesta, lo agrega, tras 240 intentos fallidos en `Poll()` lo marca como `DROPPED`, lo borra en `CleanUp()`, y **2 segundos después lo vuelve a abrir infinitamente**. Cuando por fin enciendes el mando de verdad, BetterJoy está bloqueado en medio de un timeout del nodo fantasma.
* **Solución (`ProbeAwake` antes de `Attach`)**: Al encontrar un dispositivo Bluetooth en `hid_enumerate()`, abrir el `handle` y hacer una prueba rápida (`hid_read_timeout` de 60 ms o un ping ligero). Si no llegan bytes porque el mando está dormido/apagado, cerrar el `handle` inmediatamente en silencio y **no ejecutar `Attach()`** hasta que el mando realmente esté despierto enviando paquetes.

### Causa 2: Bombardeo de lecturas SPI antes de que Bluetooth L2CAP estabilice (`NaN` en los sticks)
* **Qué pasa hoy (`Joycon.cs`, L424 y L1214–1313)**: Apenas despierta el mando por Bluetooth, está en modo básico `0x3F` y el stack Bluetooth de Windows tarda ~300–500 ms en aceptar subcomandos `0x01`. Pero `Attach()` llama en el primer milisegundo a `dump_calibration_data()` $\rightarrow$ `ReadSPI()`. Como los subcomandos SPI fallan o leen paquetes `0x3F` viejos, `stick_cal` queda lleno de ceros (`{0, 0, 0, 0, 0, 0}`). Luego, en `CenterSticks()` (L1135), el código divide `dx / t[0]` (`dx / 0 = NaN`), dejando **los sticks completamente muertos** hasta que cierras y reabres el programa.
* **Solución (`SafeAttach` con validación y fallback)**:
  1. Al detectar que el mando despertó por Bluetooth, esperar `200 ms` y vaciar el buffer de entrada (`flush`) antes de enviar subcomandos.
  2. Enviar primero `Subcommand(0x03, 0x30)` (modo completo 60 Hz) con hasta 3 reintentos verificando el ACK `0x21`.
  3. Validar siempre los datos de `stick_cal` y `acc_sensiti`/`gyr_sensiti` leídos por SPI: si cualquier rango es `<= 0` o `0xFFF`, aplicar automáticamente el fallback de calibración estándar (`0x780` / `0x830`) para que **jamás ocurra división por cero**.

### Causa 3: Windows Bluetooth exige "Quitar dispositivo y volver a emparejar"
* **Qué pasa hoy**: Los mandos de Switch tienen una peculiaridad en Windows: si el mando se apaga o pierde sincronía, Windows a menudo no reconecta el servicio HID aunque el mando esté "Emparejado", obligando al usuario a abrir *Configuración de Windows $\rightarrow$ Bluetooth $\rightarrow$ Quitar dispositivo $\rightarrow$ Agregar dispositivo*.
* **Solución (`BluetoothAutoPairService.cs` usando APIs nativas Win32 `bthprops.cpl`)**:
  * Incorporar un monitor Bluetooth en segundo plano usando P/Invoke sobre `BluetoothApis.dll` / `bthprops.cpl` (`BluetoothFindFirstDevice`, `BluetoothRemoveDevice`, `BluetoothAuthenticateDeviceEx`, `BluetoothSetServiceState` con `HumanInterfaceDeviceServiceClass_UUID`):
  * Cuando el usuario presiona el botón **Sync** en su Joy-Con o Pro Controller (poniéndolo en modo descubrimiento), BetterJoy lo detecta automáticamente por su nombre (`"Joy-Con (L)"`, `"Joy-Con (R)"`, `"Pro Controller"`), **limpia cualquier emparejamiento viejo atascado en Windows, lo empareja solo y activa el servicio HID sin que el usuario tenga que abrir la Configuración de Bluetooth de Windows**.

### Causa 4: Desconexión del mando Xbox 360 en el juego al unir el segundo Joy-Con o ante micro-cortes
* **Qué pasa hoy (`Program.cs`, L276–291)**: Cuando prendes el primer Joy-Con, se crea un mando Xbox (`out_xbox`). Cuando 3 segundos después prendes el segundo Joy-Con y se unen automáticamente, el código hace `temp.out_xbox.Disconnect(); temp.out_xbox = null;`, matando el mando Xbox del primer Joy-Con y dejando solo el del segundo. Para muchos juegos de PC, eso significa que el "Mando 1" se desconectó y ahora hay un "Mando 2", perdiendo el control de la partida.
* **Solución (Persistencia del Slot XInput Principal)**: Al unir dos Joy-Cons, **conservar siempre activo el `out_xbox` del Joy-Con que tenía el `PadId` más bajo (Jugador 1)** y desconectar el `out_xbox` secundario del segundo Joy-Con. Además, agregar un *Grace Period* (3 segundos) ante micro-cortes Bluetooth antes de destruir el dispositivo virtual de `ViGEmBus`.

---

## 4. Qué Más Agregar para que Funcione como un Producto Comercial ("Set & Forget")

1. **Modo Demonio / Inicio con Windows sin bloqueo UAC**:
   * Opción **"Iniciar con Windows (en segundo plano)"** que registra la tarea o clave de inicio para que BetterJoy arranque directo en la bandeja del sistema (System Tray). El usuario prende su PC, presiona un botón del mando de Switch y ya está jugando en Game Pass/Epic.
2. **Instalador / Verificador Integrado de Drivers (`ViGEmBus` + `HidHide`)**:
   * Al iniciar, verificar si los servicios `ViGEmBus` y `HidHide` están instalados. Si falta alguno, mostrar un banner claro en la ventana con un botón **"Instalar Driver ViGEmBus (1 clic)"** que ejecute el `.msi` incluido en `Drivers/`.
3. **Auto-Calibración Silenciosa al Conectar (*Zero-Drift Startup*)**:
   * Durante los primeros 500 ms tras conectar un mando (cuando está en reposo), muestrear automáticamente el ruido base del giroscopio para restar el *gyro drift* sin que el usuario tenga que pulsar el botón "Calibrate", y aplicar una zona muerta radial configurable (`10%–15%`) con *anti-deadzone* para que los sticks de Switch se sientan idénticos a los de un mando de Xbox Series X.

---

## 5. Tabla Resumen de Bugs Críticos Auditados

| Severidad | Archivo y Líneas | Problema Detectado (Origen) | Impacto en XInput / Conectividad | Solución Requerida |
| :--- | :--- | :--- | :--- | :--- |
| **Crítica (Bluetooth Ghost Loop)** | `Program.cs` (L116–328) | *(Repo original)* `hid_enumerate` devuelve mandos Bluetooth emparejados aunque estén apagados; BetterJoy intenta hacer `Attach()` y SPI sobre mandos dormidos cada 2 s. | Bloquea el hilo de escaneo y hace que al encender el mando tarde o falle en conectar. | Implementar `ProbeAwake()` (lectura rápida de 50 ms) antes de llamar a `Attach()`. |
| **Crítica (Sticks Muertos en BT)** | `Joycon.cs` (L424, L1135, L1214–1313) | *(Repo original)* `ReadSPI()` se ejecuta sin esperar estabilización Bluetooth; si falla devuelve `0` y `CenterSticks()` divide entre cero (`NaN`). | El mando conecta por Bluetooth pero los sticks no se mueven o el mando cae en `DROPPED`. | Pausa de estabilización BT (`200 ms`), reintento de subcomando `0x03 0x30` y fallback de calibración si `stick_cal` tiene ceros. |
| **Crítica (Concurrencia)** | `Joycon.cs` (L875–899 vs L448, L461, L1173) | *(Repo original)* `Subcommand()` escribe y lee de `handle` desde el hilo de UI mientras `Poll()` ejecuta `ReceiveRaw()` y `SendRumble()` sin sincronización. | Cambiar LEDs o unir/separar Joy-Cons desde la UI roba paquetes HID del bucle de lectura y desconecta el mando. | Encolar todo `Subcommand` en un `ConcurrentQueue` ejecutado exclusivamente por el hilo `Poll()` y proteger con `lock (_hidLock)`. |
| **Crítica (Lag Bluetooth)** | `Joycon.cs` (L154–232, L334–348, L876) | *(Repo original)* `Rumble` es un `struct` con `Queue<float[]>` no thread-safe entre ViGEm (`ReceiveRumble`) y `Poll()`, sin límite de frecuencia de envío. | Cuando un juego XInput vibra continuamente, satura el buffer Bluetooth de Windows causando hasta 1–2 s de retraso en botones y sticks. | Convertir `Rumble` a `class` con `ConcurrentQueue<float[]>`, limitar envíos a 1 paquete cada `16 ms` y omitir paquetes duplicados en reposo. |
| **Alta (Fuga de Eventos)** | `Program.cs` (L75–84, L231, L237) | *(Repo original)* `CheckForNewControllers()` hace `v.Click += form.conBtnClick` en cada conexión, pero `CleanUp()` nunca hace `-=`. | Al reconectar un Joy-Con varias veces, hacer clic para unirlos para XInput dispara el evento múltiples veces, uniéndolos y separándolos al instante. | Suscribir `conBtnClick` y `locBtnClickAsync` una sola vez en `MainForm.cs` (constructor). |
| **Alta (Conectividad 3rd Party y Grip USB)** | `Program.cs` (L28–34, L125–146) y `3rdPartyControllers.cs` (L117) | *(Repo original)* Se descartan dispositivos HID con `serial_number == null` y faltan los IDs del **Charging Grip (`0x200E`)** y **SEGA Genesis (`0x201E`)**. | Muchos mandos Pro clónicos/terceros no reportan serial y son ignorados; el Charging Grip USB oficial tampoco funciona. | Asignar serial fallback (`"000000000001"`) cuando `serial_number == null` y añadir soporte para `0x200E` y `0x201E`. |
| **Alta (Calibración N64 / DS4)** | `Joycon.cs` (L307, L1403–1421, L1622) | *(Repo original)* En `Getn64StickValues` se calcula `middleX - middleX` (siempre `0`); en `MapToDualShock4Input` (L1622) falta `else` antes de `if (isPro)`. | Rango del stick de N64 asimétrico en XInput y botones sobrescritos en DS4. | Usar `middleX` como centro real y cambiar `if (isPro)` por `else if (isPro)`. |
| **Media (ViGEm Null Crash)** | `OutputControllerXbox360.cs` (L79) y `OutputControllerDualShock4.cs` (L87) | *(Repo original)* Si el driver `ViGEmBus` no está instalado, `Program.emClient` es `null` y lanza `NullReferenceException` al conectar un mando. | La aplicación crashea en vez de avisar que falta instalar `ViGEmBus`. | Validar `Program.emClient != null` antes de crear `out_xbox` / `out_ds4` y ofrecer botón de instalación rápida del MSI incluido en `Drivers/`. |
| **Media (Ajustes .NET 8)** | `MainForm.cs` (L130), `Config.cs` (L13), `BetterJoy.csproj` (L16–25) | *(Migración .NET 8)* `Process.Start(url)` sin `UseShellExecute = true`; `Assembly.Location` en vez de `AppContext.BaseDirectory`; tags duplicados en `.csproj`. | Fallo al abrir enlaces web o al publicar como `.exe` de archivo único (`SingleFile`). | Usar `ProcessStartInfo { UseShellExecute = true }`, `AppContext.BaseDirectory` y limpiar `.csproj` / `packages.config`. |
| **Baja (UDP Server Legacy)** | `UpdServer.cs` (L183–186) | *(Repo original)* `DSUC_ListPorts` indexa `controllers[i]` sin validar `i < controllers.Count`. | Lanza `ArgumentOutOfRangeException` si se activa el servidor UDP con menos de 4 mandos. | Responder con `constate = 0` si `currRequest >= controllers.Count` y reemplazar `Crc32.NET` por `System.IO.Hashing.Crc32`. |

---

## 6. Prompt Maestro Completo para Gemini 3.8 (XInput + Auto-Bluetooth + UI Moderna)

Copia y pega este prompt en Gemini 3.8 para que implemente toda la solución de principio a fin:

```markdown
Actúa como un Ingeniero Senior de C# / .NET 8 especialista en drivers HID, Bluetooth Win32 y XInput en Windows. Estamos modernizando BetterJoy (`BetterJoyForCemu`), ya migrado a `.NET 8.0-windows`, para convertirlo en un traductor automático ("Zero-Touch Plug & Play") de mandos de Nintendo Switch (Joy-Cons, Pro Controller, NSO SNES/N64/Genesis y mandos 3rd party) a **Xbox 360 (XInput vía ViGEmBus)** para juegos de PC (Game Pass, Epic, GOG, Steam).

Ejecuta las siguientes 5 fases en orden estricto, asegurando que el proyecto compile sin errores ni warnings:

### FASE 1: Limpieza de .NET 8 y Desactivación de Peso Muerto Legacy
1. **`BetterJoy.csproj` y `App.config`**:
   - Elimina las etiquetas duplicadas `<SatelliteResourceLanguages>` y `<Version>` en `BetterJoy.csproj`.
   - Reemplaza `Crc32.NET` por `System.IO.Hashing` (v8.0.0) y usa `System.IO.Hashing.Crc32.HashToUInt32` en `UpdServer.cs`.
   - En `App.config`, elimina `<startup><supportedRuntime .../></startup>`, asegura que `ShowAsXInput` sea `true` por defecto y cambia `MotionServer` a `false` por defecto.
   - Elimina el archivo obsoleto `packages.config`.
2. **Compatibilidad .NET 8 Single-File y Eventos UI**:
   - En `Config.cs` y `3rdPartyControllers.cs`, reemplaza `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)` por `AppContext.BaseDirectory` con `Path.Combine`.
   - En `MainForm.cs` (`linkLabel1_LinkClicked`), usa `Process.Start(new ProcessStartInfo("https://paypal.me/DavidKhachaturov/5") { UseShellExecute = true })`.
   - En `Program.cs` (`Main`), agrega `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);` antes de `Application.EnableVisualStyles();`.
   - En `Program.cs` (`CheckForNewControllers`), elimina las suscripciones repetidas `v.Click += form.conBtnClick` y `form.loc[ii].Click += form.locBtnClickAsync`, y muévelas una sola vez al constructor de `MainForm.cs`.

### FASE 2: Reconocimiento Automático, Handshake Bluetooth Robusto y Auto-Pairing
1. **Filtro Anti-Fantasmas Bluetooth (`ProbeAwake`) en `Program.cs`**:
   - Windows mantiene los nodos HID de mandos Bluetooth emparejados aunque estén apagados. En `CheckForNewControllers()`, cuando abras `IntPtr handle = HIDapi.hid_open_path(enumerate.path)` para un dispositivo Bluetooth (`serial_number != "000000000001"`), haz primero una prueba rápida (`ProbeDeviceAwake(handle)`): intenta leer un reporte con `hid_read_timeout(handle, buf, 49, 40)`. Si devuelve `<= 0`, cierra `hid_close(handle)` en silencio y continúa con el siguiente dispositivo sin instanciar `Joycon` ni bloquear el hilo.
2. **Handshake Bluetooth Estable (`SafeAttach`) y Fallback de Calibración en `Joycon.cs`**:
   - En `Attach()`, si no es USB (`!isUSB`), espera `150 ms` de estabilización tras despertar y vacía el buffer de lectura (`while (HIDapi.hid_read_timeout(handle, a, 64, 5) > 0) {}`) antes de llamar a `dump_calibration_data()`.
   - Envía `Subcommand(0x03, new byte[] { 0x30 }, 1)` hasta 3 veces si la respuesta no es `0x21`.
   - En `dump_calibration_data()`, después de leer `stick_cal` y `stick2_cal` por SPI, verifica si alguno de los valores de rango (`stick_cal[0]`, `stick_cal[1]`, `stick_cal[4]`, `stick_cal[5]`) es `0` o `>= 0xFFF` (lectura SPI fallida por Bluetooth). Si es así, carga automáticamente los valores de respaldo (`0x780, 0x780, 0x780, 0x830, 0x780, 0x780` y `deadzone = 200`) para que `CenterSticks()` jamás divida entre cero (`NaN`).
   - En `Poll()`, cuando `a == 0` (lectura no bloqueante vacía), agrega `Thread.Sleep(1)` para no consumir el 100% de un núcleo de CPU.
3. **Servicio de Auto-Emparejamiento Bluetooth (`BluetoothAutoPairService.cs`)**:
   - Crea una clase `BluetoothAutoPairService` usando P/Invoke de `BluetoothApis.dll` (`BluetoothFindFirstRadio`, `BluetoothFindFirstDevice`, `BluetoothFindNextDevice`, `BluetoothRemoveDevice`, `BluetoothAuthenticateDeviceEx`, `BluetoothSetServiceState` con GUID HID `00001124-0000-1000-8000-00805f9b34fb`).
   - Permite escanear en segundo plano mandos de Switch en modo Sync (`"Joy-Con (L)"`, `"Joy-Con (R)"`, `"Pro Controller"`): si encuentra uno no recordado o desconectado pero visible en modo emparejamiento, lo autentica y habilita su servicio HID automáticamente sin que el usuario tenga que abrir la Configuración de Bluetooth de Windows.
4. **Detección Instantánea por `WM_DEVICECHANGE` y Preservación del Jugador 1 en XInput**:
   - En `MainForm.cs`, sobrescribe `WndProc` para escuchar `WM_DEVICECHANGE` y disparar un escaneo inmediato al enchufar o encender un mando.
   - Al unir automáticamente dos Joy-Cons en `Program.cs` (`foundNew`), conserva siempre el `out_xbox` del Joy-Con con menor `PadId` (Jugador 1) y desconecta el `out_xbox` del segundo Joy-Con, de modo que el juego en curso nunca pierda el mando 1.

### FASE 3: Concurrencia HID, Anti-Lag de Rumble y Soporte Extendido de Mandos
1. **Cola Thread-Safe de Subcomandos y Rumble en `Joycon.cs`**:
   - Crea `private readonly ConcurrentQueue<Action> _pendingSubcommands = new();` en `Joycon` para que llamadas desde la UI (`SetPlayerLED`, `SetHomeLight`, `BlinkHomeLight`) cuando `!stop_polling` se ejecuten dentro del hilo `Poll()`.
   - En `Detach()`, asigna `stop_polling = true;` y espera con `PollThreadObj?.Join(300);` antes de cerrar `handle`.
   - Convierte `struct Rumble` en `sealed class Rumble` con `ConcurrentQueue<float[]>`, limitando el envío de paquetes de vibración en `Poll()` a un máximo de 1 paquete cada `16 ms` y omitiendo paquetes de amplitud `0` repetidos.
2. **Soporte para Charging Grip (`0x200e`), Genesis (`0x201e`), N64 y 3rd Party**:
   - En `Program.cs`, añade `product_grip = 0x200e` y `product_genesis = 0x201e`. Para `0x200e`, lee el byte `a[3]` del comando USB `0x80 0x01` (`0x01` = Left, `0x02` = Right).
   - No descartes dispositivos con `enumerate.serial_number == null`; usa `"000000000001"` como fallback para Nintendo (`0x057e`) o `thirdPartyCons`.
   - Corrige `Getn64StickValues` reemplazando `(middleX - middleX)` y `(middleX + middleX)` por `middleX`, y cambia `if (isPro)` por `else if (isPro)` en `MapToDualShock4Input` (L1622).
   - Valida `Program.emClient != null` antes de instanciar `OutputControllerXbox360` / `OutputControllerDualShock4`, y valida `currRequest < controllers.Count` en `UpdServer.cs`.

### FASE 4: Integración con HidHide (Ocultar Mando Nativo para Evitar Doble Input)
1. Crea `HidHideService.cs` que localice `HidHideCLI.exe` y oculte automáticamente los Device Instance Paths de los mandos de Switch conectados (`--app-reg`, `--dev-hide`, `--cloak-on`), desocultándolos al salir (`--dev-unhide`). Reemplaza `WebRequest.Create` por `HttpClient`.

### FASE 5: Interfaz Moderna Centrada en XInput y Recarga en Caliente
1. Rediseña `MainForm` con tema oscuro moderno, soporte High-DPI, indicador de estado de `ViGEmBus` / `HidHide`, botón de `"Auto-Pair Bluetooth"`, e inicio opcional con Windows.
2. Muestra tarjetas de mando con tipo, modo (`Xbox 360 XInput`), enlace (`USB` / `BT`), nivel de batería y botones rápidos (`Unir/Separar`, `Vibrar`, `Calibrar`).
3. Reemplaza los `TextBox` crudos de configuración por controles tipados (`CheckBox`, `ComboBox`, `NumericUpDown`) y aplica los cambios en caliente sobre los mandos conectados sin llamar a `Application.Restart()`.
```
