# 🌌 Serena VR — Los Sueños de Grete Stern (1948 - 1951)

Experiencia inmersiva e interactiva de Realidad Virtual para **Google Cardboard (Android)** y **PC**, inspirada en la vanguardista serie de fotomontajes surrealistas *«Los Sueños»* de la fotógrafa y artista **Grete Stern**.

El proyecto combina realidad virtual estereoscópica para dispositivos móviles con un sistema de **visión artificial y tracking de manos por cámara web** (MediaPipe + extOSC), permitiendo manipular el entorno onírico mediante gestos naturales sin necesidad de costosos mandos de VR.

---

## 🎭 Estructura Narrativa de la Obra

El videojuego se divide en tres niveles interconectados que recorren la tensión, el mito y la liberación femenina presentes en los fotomontajes de Grete Stern:

```
[ Nivel 1: La Habitación Onírica ]
       │  (Contemplación, miradas y manos emergentes)
       ▼
[ Nivel 2: La Cueva de Sísifo y Sistemas Complejos ]
       │  (Empuje de la roca, tensión de la soga, estallido flocking)
       ▼
[ Nivel 3: La Puerta y La Emancipación ]
          (Contemplación de la llave, apertura y gigantografía final)
```

### 1. Nivel 1 — La Habitación de los Ojos y las Manos
* Espacio cerrado conformado por los emblemáticos fotomontajes de Grete Stern (*Los ojos de la montaña*, *Las manos del mar*).
* Desplazamiento por el plano onírico guiado por la mirada y la interacción gestual.

### 2. Nivel 2 — El Mito de Sísifo y Sistemas Complejos
* Inspirado en el mito de Sísifo y la escultura *La mujer y la roca*: el usuario empuja la roca montaña arriba mediante esfuerzo gestual coordinado con la tensión física de la soga.
* Al alcanzar la cumbre, la soga se corta con chasquido sonoro y la escultura estalla en decenas de fragmentos que orbitan bajo algoritmos de **Boids / Flocking** (sistemas complejos autoorganizados) antes de descender libremente al abismo.

### 3. Nivel 3 — La Puerta, La Llave y La Emancipación
* Al despejarse la montaña, un sendero de piedra conduce hacia una puerta aislada en medio del paisaje y un pedestal iluminado.
* **Paso 1 (Contemplación de la llave):** 3 segundos de contemplación solemne de la llave dorada sobre el pedestal.
* **Paso 2 (Interacción):** El usuario se aproxima y agarra la llave con el gesto de pinza o botón del visor.
* **Paso 3 (Apertura):** Al acercarse a la puerta, la llave encastra en la cerradura, gira con chasquido mecánico, la puerta se abre de par en par y la llave se desvanece.
* **Paso 4 (Contemplación de la obra):** Cruce del umbral hacia el claro para contemplar durante 3 segundos la gigantografía de cuerpo entero de Grete Stern con la llave.
* **Paso 5 (Culminación):** Aparición sincronizada de la leyenda dorada sobre recuadro negro:
  > **GRETE STERN**  
  > *Los Sueños (1948 - 1951)*  
  > ★ LA EMANCIPACIÓN ★  
  > Obra Completada

---

## 🛠️ Arquitectura Técnica

* **Motor Gráfico:** Unity 6 (6000.4.11f1) URP (Universal Render Pipeline).
* **Visor VR:** Google Cardboard XR Plugin para Android con soporte híbrido de seguimiento (giroscopio de hardware + AttitudeSensor para dispositivos sin giroscopio dedicado).
* **Tracking de Manos (Webcam):**
  * Script en Python (`vision_core.py`) utilizando **Google MediaPipe Hands** a 60 FPS.
  * Reconocimiento de 21 landmarks tridimensionales, detección de gesto pinza (índice-pulgar), puño cerrado, palma abierta y signo de paz.
  * Comunicación en red local en tiempo real por protocolo **OSC (Open Sound Control)** vía UDP puerto 5005 con `extOSC`.
* **Input Universal:** Compatible con visores Cardboard con o sin botón físico (disparo por toque en pantalla, trigger de Cardboard, gestos de MediaPipe y detección por proximidad inteligente).

---

## 🚀 Requisitos e Instalación

### 1. Teléfono Android (Visor VR)
* Teléfono Android con giroscopio o acelerómetro.
* Visor tipo Google Cardboard o similar.
* Instalar el archivo `Builds/Serena_Cardboard_Release.apk`.

### 2. PC (Tracking por Webcam)
1. Instalar Python 3.9 o superior.
2. Navegar a la carpeta `UnityVRTracking` e instalar dependencias:
   ```bash
   pip install -r requirements.txt
   ```
   *(O ejecutar `INSTALAR_DEPENDENCIAS.bat` en Windows)*.
3. Iniciar el tracking:
   ```bash
   python vision_core.py
   ```
   *(O ejecutar `INICIAR_TRACKING.bat`)*.
4. Asegurarse de que el teléfono y la PC estén conectados a la **misma red Wi-Fi**.

### 3. Compilación desde Unity
1. Abrir el proyecto en **Unity 6**.
2. En la barra superior de menús, seleccionar:
   `Serena VR` ➔ `2. Compilar APK Release Limpio`.

---

## 📁 Estructura del Repositorio

```
├── Assets/
│   ├── Nivel1_Ojo/               # Escena y shaders de la habitación inicial
│   ├── Nivel2_Montana/           # Terreno, rocas físicas, soga y lógica de Sísifo
│   ├── Nivel3_Puerta/            # Lógica de la llave, pedestal, puerta y cierre
│   ├── EsculturaGrete3D/         # Modelo 3D y algoritmo flocking de fragmentos
│   ├── extOSC/                   # Módulo de comunicación OSC de baja latencia
│   └── PolyOne/                  # Mano 3D anatómica procedural para VR
├── Packages/                     # Manifiesto de paquetes de Unity (Cardboard XR, URP)
├── ProjectSettings/              # Configuración de renderizado, tags y XR
├── UnityVRTracking/              # Servidor Python de MediaPipe Hands + OSC
├── .gitignore                    # Reglas optimizadas para proyectos Unity
└── .gitattributes                # Configuración de Git LFS para modelos 3D grandes
```

---

## 📜 Créditos y Autoría
* **Inspiración artística:** Grete Stern — *Serie Los Sueños* (Revista Idilio, 1948 - 1951).
* **Desarrollo y Diseño Interactivo:** Serena VR.
