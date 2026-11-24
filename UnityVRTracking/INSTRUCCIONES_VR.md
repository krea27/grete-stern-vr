# 🌌 Serena VR - Guía de Instalación y Ejecución

Guía paso a paso para ejecutar y probar la experiencia de realidad virtual **Serena VR** (inspirada en los fotomontajes de *Grete Stern*) utilizando un teléfono móvil Android en un visor tipo **Google Cardboard** y tracking de manos en tiempo real por **Webcam + MediaPipe** desde la PC.

---

## 📦 1. Contenido del Paquete

Para ejecutar la experiencia solo se necesitan dos cosas:

| Elemento | Archivo / Carpeta | Dónde se ejecuta |
| :--- | :--- | :--- |
| **App de Realidad Virtual** | `Serena_Cardboard_Fixed.apk` | Teléfono Android |
| **Tracking de Manos (Webcam)** | Carpeta `UnityVRTracking` | PC con cámara web |

---

## 📱 2. Configuración en el Teléfono (Android)

1. **Conexión a la Red**: Asegurarse de que el teléfono esté conectado a la **misma red Wi-Fi** que la PC.
2. **Instalar la APK**:
   - Copiar el archivo `Serena_Cardboard_Fixed.apk` a la memoria del teléfono (por cable USB, Google Drive, WhatsApp Web o Telegram).
   - Abrir el archivo `.apk` e instalarlo. Si Android solicita confirmación, activar *"Permitir instalar aplicaciones de fuentes desconocidas"*.
3. **Colocar en el Visor**:
   - Abrir la aplicación **Serena VR**.
   - La pantalla se dividirá en dos (modo estéreo para VR).
   - Introducir el teléfono en el visor Cardboard en posición horizontal (con la parte superior del teléfono hacia la izquierda).

---

## 💻 3. Configuración en la PC (Tracking de Manos)

### Requisitos previos
- Tener instalado **Python 3.9, 3.10, 3.11 o 3.12** en Windows ([python.org](https://www.python.org/downloads/)).
  > ⚠️ *Al instalar Python, marcar la casilla: **"Add Python to PATH"**.*
- Cámara web conectada y libre (cerrar Zoom, Discord, Meet o Teams para que no ocupen la cámara).

### Pasos para iniciar:
1. Abrir la carpeta `UnityVRTracking`.
2. **Primera vez**: Hacer doble clic en:
   ```bash
   INSTALAR_DEPENDENCIAS.bat
   ```
   *(Esto instalará automáticamente OpenCV, MediaPipe y Python-OSC en menos de un minuto).*
3. **Para iniciar el tracking**: Hacer doble clic en:
   ```bash
   INICIAR_TRACKING.bat
   ```
4. Se abrirá una ventana de visualización mostrando la cámara web en espejo con el esqueleto de 21 articulaciones de tu mano dibujado en tiempo real.

---

## 🎮 4. Cómo Interactuar en la Experiencia

| Acción | Cómo se hace | Qué sucede en la experiencia |
| :--- | :--- | :--- |
| **Exploración 360°** | Girar o inclinar la cabeza con el visor | La cámara rota en 360° de forma fluida (arriba, abajo y a los lados). |
| **Control de la Mano** | Mover tu mano real frente a la webcam | Una mano 3D articulada acompaña tus movimientos dentro del campo visual. |
| **Articulación de Dedos** | Abrir, cerrar o mover dedos individuales | Cada falange de los 5 dedos responde 1:1 en tiempo real. |
| **Profundidad (Eje Z)** | Acercar o alejar la mano de la webcam | La mano 3D avanza en profundidad hacia los objetos y la pared. |
| **Colapso con el Ojo** | Mirar la pared izquierda y acercar la mano a `portal_ojo` | La mano se ilumina en **celeste brillante**, indicando que está tocando el ojo. |
| **Escapar del Cubo** | Hacer el gesto de **pinza** (juntar pulgar e índice) mientras tocás el ojo | La mano se ilumina en **verde brillante** y te teletransporta al exterior del cubo. |

---

## ❓ 5. Solución de Problemas Frecuentes

- **La mano no aparece o no se mueve en el celular**:
  - Verificar que el teléfono y la PC estén en la **misma red Wi-Fi**.
  - Si el router bloquea transmisiones broadcast, se puede indicar la IP del celular ejecutando en la consola de la PC:
    ```bash
    python vision_core.py --ip 192.168.X.X
    ```
    *(Reemplazando `192.168.X.X` por la IP local del teléfono).*
  - Asegurarse de que el Firewall de Windows no esté bloqueando el tráfico UDP en el puerto `5005`.

- **La ventana de la cámara en la PC se abre en negro**:
  - Asegurarse de que ninguna otra aplicación (navegador, Meet, Discord) esté usando la webcam.
  - Si la PC tiene más de una cámara (ej. integrada y externa), se puede cambiar con:
    ```bash
    python vision_core.py --cam 1
    ```

- **Recentrar el horizonte en VR**:
  - Tocar la pantalla del teléfono o presionar el gatillo magnético del Cardboard durante un instante para reorientar el frente de la vista.

- **Cerrar el programa de tracking**:
  - Presionar la tecla `q`, `ESC` o hacer clic en la `X` de la ventana de la cámara en la PC.

