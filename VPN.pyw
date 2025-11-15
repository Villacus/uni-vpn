import pyautogui
import time
import os
import pyperclip
import subprocess
import pygetwindow as gw
import json

# Cargar credenciales desde archivo externo
def cargar_credenciales():
    ruta_config = os.path.join(os.path.dirname(__file__), "credentials.json")
    if not os.path.exists(ruta_config):
        print("❌ Error: No se encuentra credentials.json")
        return None
    
    with open(ruta_config, 'r') as f:
        return json.load(f)

config = cargar_credenciales()
if not config:
    exit(1)

# Abrir FortiClient VPN
forticlient_path = config.get("forticlient_path", r"C:\Program Files\Fortinet\FortiClient\FortiClientConsole.exe")
subprocess.Popen([forticlient_path], shell=True)

time.sleep(5)

# Asumimos que hay un perfil llamado "EHU" guardado en FortiClient
# Pulsa TAB unas veces hasta llegar al botón "Conectar" (puedes ajustar el nº de TABs)

pyautogui.press('tab')
pyautogui.press('tab')
pyautogui.press('tab')

# Introducir usuario y contraseña
usuario = config["usuario"]
password = config["password"]
pyautogui.typewrite(usuario)
pyautogui.press('tab')
pyautogui.typewrite(password)
pyautogui.press('enter')
time.sleep(0.5)

# Abrir comet para la autenticación 2FA
os.system("start comet")
time.sleep(3)

# Activar la extensión de Authenticator en comet y copiar el código
pyautogui.hotkey("ctrl", "shift", "a")  # Ajusta si usas otro atajo
time.sleep(0.2)
pyautogui.hotkey("ctrl", "shift", "e")  # Ajusta si usas otro atajo
pyautogui.press("enter")
time.sleep(0.2)
pyautogui.hotkey("ctrl", "w")

# Pega el código 2FA en FortiClient
fc_window = gw.getWindowsWithTitle("FortiClient - Zero Trust Fabric Agent")
fc_window[0].activate()
time.sleep(0.8)

twofa_code = pyperclip.paste()
if not twofa_code:
    print("⚠ Error: No se copió el código 2FA. Por favor, verifica la extensión.")
else:
    pyautogui.typewrite(twofa_code)
    pyautogui.press("enter")