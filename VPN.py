import pyautogui
import time
import os
import pyperclip
import subprocess
import pygetwindow as gw

# 1️⃣ Abrir FortiClient VPN
forticlient_path = r"C:\Program Files\Fortinet\FortiClient\FortiClientConsole.exe"
subprocess.Popen([forticlient_path], shell=True)

time.sleep(5)  # ¡Dale más tiempo! FortiClient tarda más en abrir que AnyConnect

# 2️⃣ Asumimos que hay un perfil llamado "EHU" guardado en FortiClient
# Pulsa TAB unas veces hasta llegar al botón "Conectar" (puedes ajustar el nº de TABs)

pyautogui.press('tab')
pyautogui.press('tab')
pyautogui.press('tab')

# 3️⃣ Introducir usuario y contraseña
usuario = "1209104 "           # <- Cambia aquí tu usuario
password = "Kerejeta4!!"         # <- Cambia aquí tu password
pyautogui.typewrite(usuario)
pyautogui.press('tab')
pyautogui.typewrite(password)
pyautogui.press('enter')
time.sleep(0.5)

# 4️⃣ Abrir Chrome para la autenticación 2FA
os.system("start comet")
time.sleep(0.8)  # Esperar a que cargue la ventana

# 5️⃣ Activar la extensión de Authenticator en Chrome y copiar el código
pyautogui.hotkey("ctrl", "shift", "a")  # Ajusta si usas otro atajo
time.sleep(0.2)
pyautogui.hotkey("ctrl", "shift", "e")  # Ajusta si usas otro atajo
pyautogui.press("enter")
time.sleep(0.1)
pyautogui.hotkey("ctrl", "w")

# 6️⃣ Pega el código 2FA en FortiClient
fc_window = gw.getWindowsWithTitle("FortiClient - Zero Trust Fabric Agent")
fc_window[0].activate()
time.sleep(0.5)

twofa_code = pyperclip.paste()
if not twofa_code:
    print("⚠ Error: No se copió el código 2FA. Por favor, verifica la extensión.")
else:
    pyautogui.typewrite(twofa_code)
    pyautogui.press("enter")