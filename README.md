# VPN Connection Automation 🔐

Automatización de conexión a VPN con autenticación de dos factores (2FA) para **FortiClient**.

## 📋 Descripción

Este script automatiza el proceso de:
1. Abre FortiClient VPN
2. Ingresa automáticamente usuario y contraseña
3. Obtiene el código 2FA desde una extensión de autenticador
4. Introduce el código 2FA en FortiClient
5. Completa la conexión a la VPN

## 🛠 Requisitos

### Dependencias Python
```bash
pip install pyautogui pyperclip pygetwindow
```

### Software necesario
- **FortiClient** instalado en `C:\Program Files\Fortinet\FortiClient\FortiClientConsole.exe`
- **Navegador con extensión Authenticator** (Chrome/Comet)
- **Python 3.7+**

## 📁 Estructura del Proyecto

```
VPN/
├── VPN.py                      # Versión ejecutable en terminal
├── VPN.pyw                     # Versión silenciosa (sin ventana de consola)
├── credentials.json            # ⚠️ NO SUBIR A GITHUB (ignorado en .gitignore)
├── credentials.example.json    # Plantilla de configuración
├── README.md                   # Este archivo
└── .gitignore                  # Archivos a ignorar en Git
```

## 🔧 Configuración

### 1. Crear el archivo de credenciales

Copia `credentials.example.json` y crea `credentials.json`:

```bash
copy credentials.example.json credentials.json
```

### 2. Completar `credentials.json`

```json
{
  "usuario": "TU_USUARIO_AQUI",
  "password": "TU_CONTRASEÑA_AQUI",
  "forticlient_path": "C:\\Program Files\\Fortinet\\FortiClient\\FortiClientConsole.exe"
}
```

**Importante:** Este archivo está en `.gitignore` y nunca se subirá a GitHub.

## ▶️ Uso

### Método recomendado: Usar el acceso directo

1. Copia `VPN.lnk` a otro lugar (Escritorio, Menú Inicio, etc.)
2. Haz doble clic en `VPN.lnk` para ejecutar

### Alternativas


Opción 2: Ejecutar silenciosamente

```bash
python VPN.pyw
```

## 🔐 Seguridad

- ✅ Las credenciales se almacenan en `credentials.json` (ignorado por Git)
- ✅ El repositorio público no expone datos sensibles
- ✅ Solo debes compartir `credentials.example.json` como referencia
- ✅ Cada usuario crea su propio `credentials.json` localmente

### Recomendaciones adicionales

1. **Nunca commits credenciales reales** al repositorio
2. **Verifica el .gitignore** antes de hacer push
3. **Usa contraseñas seguras** en tu `credentials.json` local
4. **Considera usar variables de entorno** en producción

## 🔄 Flujo de Automatización

```
1. Cargar credenciales desde credentials.json
   ↓
2. Abrir FortiClient VPN
   ↓
3. Navegar a campos de login (3x TAB)
   ↓
4. Ingresar usuario y contraseña
   ↓
5. Abrir navegador (Comet)
   ↓
6. Activar extensión Authenticator
   ↓
7. Copiar código 2FA al portapapeles
   ↓
8. Cerrar navegador
   ↓
9. Pegar código 2FA en FortiClient
   ↓
10. Conectado ✓
```

## ⚙️ Personalización

### Ajustar tiempos de espera
Si el script va demasiado rápido o lento, modifica los `time.sleep()`:

```python
time.sleep(5)    # Aumentar si FortiClient tarda más en abrir
time.sleep(3)    # Tiempo para que cargue el navegador
time.sleep(0.8)  # Tiempo entre acciones
```

### Cambiar atajos del Authenticator
Si usas otros atajos de teclado en tu extensión:

```python
pyautogui.hotkey("ctrl", "shift", "a")  # Ajusta estos valores
pyautogui.hotkey("ctrl", "shift", "e")  # Según tu extensión
```

### Cambiar nombre de ventana de FortiClient
Si tu ventana tiene otro nombre, modifica:

```python
fc_window = gw.getWindowsWithTitle("FortiClient - Zero Trust Fabric Agent")
```

## 🐛 Solución de Problemas

### Error: "No se encuentra credentials.json"
- Verifica que `credentials.json` exista en la misma carpeta que el script
- Copia desde `credentials.example.json` si es necesario

### El script va muy rápido/lento
- Ajusta los valores de `time.sleep()` según tu sistema

### FortiClient no responde
- Asegúrate de que FortiClient esté instalado en la ruta correcta
- Verifica que el perfil "EHU" esté configurado en FortiClient

### El código 2FA no se copia
- Verifica que la extensión Authenticator esté activada
- Comprueba que los atajos de teclado sean correctos
- Aumenta el tiempo de espera: `time.sleep(0.5)` → `time.sleep(1)`

## 📝 Historial de cambios

- **v1.1**: Implementado sistema de credenciales externas
- **v1.0**: Versión inicial con credenciales hardcodeadas

## 📄 Licencia

Este proyecto es de uso personal.

## ✍️ Autor

Villacus

---

**Última actualización:** Noviembre 2025
