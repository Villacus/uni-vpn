# VPN Connection Automation 🔐

Automatización de conexión a VPN con autenticación de dos factores (2FA) para **FortiClient**.

---

## 🆕 C# / .NET 8 — Nuevo enfoque (recomendado)

La versión C# reemplaza los `time.sleep()` del script Python por detección de estado
real vía **Windows UI Automation (UIA)** con timeouts configurables, reintentos y
logging estructurado.

### 📁 Estructura del proyecto C#

```
src/UniVpn.Automation/
├── UniVpn.Automation.sln
├── UniVpn.Automation/          # Ejecutable Windows (.NET 8 Windows)
│   ├── Program.cs
│   └── Automation/
│       ├── WindowsUiaDetector.cs   # Detección de estado vía UIA
│       ├── FortiClientAutomation.cs
│       ├── CredentialProvider.cs   # Env vars / Windows Credential Manager
│       └── ICredentialProvider.cs
├── UniVpn.Automation.Core/     # Biblioteca multiplataforma (lógica pura)
│   ├── Configuration/
│   │   ├── AppConfig.cs
│   │   └── UiSelectors.cs
│   └── StateMachine/
│       ├── VpnState.cs
│       ├── VpnStateMachine.cs
│       └── IWindowDetector.cs
└── UniVpn.Automation.Tests/    # Tests unitarios (xUnit + Moq)
    ├── AppConfigTests.cs
    └── VpnStateMachineTests.cs
config.example.json             # Plantilla de configuración
```

### 🛠 Requisitos

- **.NET 8 SDK** (o superior): <https://dotnet.microsoft.com/download>
- **Windows** (el ejecutable es exclusivo de Windows; los tests se pueden ejecutar en cualquier plataforma)
- **FortiClient** instalado

### 🔧 Configuración

1. Copia la plantilla:

   ```bash
   copy config.example.json config.json
   ```

2. Edita `config.json` con tu ruta de FortiClient y ajusta los selectores si es necesario.  
   **No almacenes credenciales en el archivo de configuración.**

3. Proporciona las credenciales mediante variables de entorno (por defecto):

   ```powershell
   $env:UNI_VPN_USERNAME = "tu_usuario"
   $env:UNI_VPN_PASSWORD = "tu_contraseña"
   # Sólo si se pide 2FA:
   $env:UNI_VPN_2FA_TOKEN = "123456"
   ```

   O usando **Windows Credential Manager** (recomendado para entornos permanentes):

   ```cmd
   cmdkey /generic:UniVpn/FortiClient /user:tu_usuario /pass:tu_contraseña
   ```

   Y establece `"CredentialMode": "WindowsCredentialManager"` en `config.json`.

### ▶️ Compilar y ejecutar

**Compilar:**

```bash
cd src/UniVpn.Automation
dotnet build -c Release
```

**Publicar como ejecutable único (recomendado para distribución):**

```bash
dotnet publish .\UniVpn.Automation\UniVpn.Automation.csproj -c Release -r win-x64 -o .\publish\ /p:PublishSingleFile=true /p:SelfContained=true
```

El resultado será `publish/UniVpn.Automation.exe`.

**Ejecutar:**

```powershell
# Modo normal (conectar)
.\UniVpn.Automation.exe

# Con configuración personalizada
.\UniVpn.Automation.exe --config "C:\ruta\config.json"

# Modo dry-run: sólo detecta y muestra el estado actual, sin enviar teclas
.\UniVpn.Automation.exe --dry-run
```

**Códigos de salida:**

| Código | Significado |
|--------|-------------|
| 0 | Éxito / conectado |
| 1 | Error de configuración |
| 2 | Conexión fallida |
| 3 | Tiempo de espera global agotado |
| 4 | Excepción no controlada |

### 🧪 Ejecutar tests

```bash
cd src/UniVpn.Automation
dotnet test -c Debug
```

Los tests son multiplataforma y no requieren FortiClient ni Windows.

### 🔄 Estados detectados

```
NotRunning  → FortiClient no está en ejecución o ventana no encontrada
Starting    → FortiClient arrancando, esperando ventana
Disconnected → Ventana visible, desconectado / inactivo
CredentialsRequired → Campos de usuario/contraseña visibles y activos
TwoFactorRequired   → Campo de token 2FA visible y activo
Connecting  → Conexión en progreso
Connected   → VPN conectada
Error       → FortiClient muestra un mensaje de error
Timeout     → Tiempo de espera agotado
```

### 🔐 Seguridad

- ✅ Las credenciales **nunca** se almacenan en el código ni en el fichero de configuración
- ✅ Se leen de variables de entorno o del Windows Credential Manager
- ✅ `config.json` está en `.gitignore`
- ✅ El modo `UseFallbackSendKeys` (teclas simuladas) está desactivado por defecto

---

## 🐍 Enfoque Python original (legacy)

> El script Python original se mantiene como referencia. Se recomienda usar el
> enfoque C# para mayor robustez.

### Descripción

El script automatiza el proceso de:
1. Abre FortiClient VPN
2. Ingresa automáticamente usuario y contraseña
3. Obtiene el código 2FA desde una extensión de autenticador
4. Introduce el código 2FA en FortiClient
5. Completa la conexión a la VPN

### Dependencias Python

```bash
pip install pyautogui pyperclip pygetwindow
```

### Software necesario

- **FortiClient** instalado en `C:\Program Files\Fortinet\FortiClient\FortiClientConsole.exe`
- **Navegador con extensión Authenticator** (Chrome/Comet)
- **Python 3.7+**

### Configuración legacy

Copia `credentials.example.json` y crea `credentials.json`:

```bash
copy credentials.example.json credentials.json
```

Completa `credentials.json`:

```json
{
  "usuario": "TU_USUARIO_AQUI",
  "password": "TU_CONTRASEÑA_AQUI",
  "forticlient_path": "C:\\Program Files\\Fortinet\\FortiClient\\FortiClientConsole.exe"
}
```

**Importante:** Este archivo está en `.gitignore` y nunca se subirá a GitHub.

### Uso legacy

```bash
python VPN.pyw
```

### Limitaciones conocidas

- Usa `time.sleep()` con tiempos fijos → falla si FortiClient tarda más de lo esperado
- No detecta el estado real de la UI

---

## 📝 Historial de cambios

- **v2.0**: Migración a C# / .NET 8 con Windows UI Automation, máquina de estados y tests
- **v1.1**: Implementado sistema de credenciales externas (Python)
- **v1.0**: Versión inicial con credenciales hardcodeadas (Python)

## 📄 Licencia

Este proyecto es de uso personal.

## ✍️ Autor

Villacus

---

**Última actualización:** Marzo 2026
