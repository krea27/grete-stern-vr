import argparse
import math
import os
import signal
import socket
import sys
import time
import cv2  # type: ignore
import mediapipe as mp  # type: ignore
from mediapipe.tasks import python  # type: ignore
from mediapipe.tasks.python import vision  # type: ignore
from pythonosc import udp_client  # type: ignore

WINDOW_NAME = "Serena VR - MediaPipe Hand Tracking (OSC)"

def get_network_ips():
    """Detecta la IP local de la PC y la dirección broadcast de la subred."""
    local_ip = "127.0.0.1"
    broadcast_ip = "255.255.255.255"
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        local_ip = s.getsockname()[0]
        s.close()
        parts = local_ip.split('.')
        broadcast_ip = f"{parts[0]}.{parts[1]}.{parts[2]}.255"
    except Exception:
        pass
    return local_ip, broadcast_ip

parser = argparse.ArgumentParser(description="MediaPipe VR Hand Tracking para Unity Serena VR")
parser.add_argument("--ip", type=str, default=None, help="IP específica del teléfono (opcional)")
parser.add_argument("--cam", type=int, default=0, help="Índice de la cámara (por defecto 0)")
parser.add_argument("--port", type=int, default=5005, help="Puerto OSC (por defecto 5005)")
args = parser.parse_args()

local_ip, subnet_broadcast = get_network_ips()

target_ips = ["127.0.0.1", subnet_broadcast]
if args.ip and args.ip not in target_ips:
    target_ips.append(args.ip)

print("=" * 65)
print("  SERENA VR - TRACKING DE MANOS (MediaPipe + OSC)")
print("=" * 65)
print(f"[*] IP de esta PC: {local_ip}")
print(f"[*] Puerto OSC: {args.port}")
print(f"[*] Transmitiendo a:")
for ip in target_ips:
    print(f"    -> {ip}:{args.port}")
print("=" * 65)

class MultiOSCClient:
    def __init__(self, ips, port):
        self.clients = []
        for ip in ips:
            try:
                c = udp_client.SimpleUDPClient(ip, port, allow_broadcast=True)
                self.clients.append((ip, c))
            except Exception as e:
                print(f"[!] Aviso al configurar {ip}: {e}")

    def send_message(self, address, value):
        for ip, client in self.clients:
            try:
                client.send_message(address, value)
            except Exception:
                pass

osc_client = MultiOSCClient(target_ips, args.port)

def calculate_distance(p1, p2):
    return math.sqrt((p1.x - p2.x)**2 + (p1.y - p2.y)**2)

# Inicializar MediaPipe
script_dir = os.path.dirname(os.path.abspath(__file__))
model_path = os.path.join(script_dir, 'hand_landmarker.task')

if not os.path.exists(model_path):
    print(f"[ERROR] No se encontró 'hand_landmarker.task' en: {script_dir}")
    sys.exit(1)

base_options = python.BaseOptions(model_asset_path=model_path)
options = vision.HandLandmarkerOptions(
    base_options=base_options,
    running_mode=vision.RunningMode.VIDEO,
    num_hands=1,
    min_hand_detection_confidence=0.5,
    min_hand_presence_confidence=0.5,
    min_tracking_confidence=0.5
)
detector = vision.HandLandmarker.create_from_options(options)

# Conectar la cámara de forma directa y limpia
print(f"[*] Abriendo cámara {args.cam} con DirectShow...")
cap = cv2.VideoCapture(args.cam, cv2.CAP_DSHOW)

# Si falla la cámara seleccionada, probar la otra (0 o 1)
if not cap.isOpened():
    fallback_idx = 1 if args.cam == 0 else 0
    print(f"[!] No abrió cámara {args.cam}, intentando cámara alternativa {fallback_idx}...")
    cap = cv2.VideoCapture(fallback_idx, cv2.CAP_DSHOW)

if not cap.isOpened():
    print("[ERROR] No se pudo abrir ninguna cámara.")
    print("Asegurate de que ninguna otra aplicación (Discord, Zoom, Chrome, Iriun) esté usando la webcam.")
    sys.exit(1)

# Resolución 640x480 a 60 FPS: elimina el delay de captura USB y triplica la velocidad de MediaPipe
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
cap.set(cv2.CAP_PROP_FPS, 60)
cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

print("[OK] Cámara conectada exitosamente (Modo Baja Latencia 640x480).")
print("[*] Instrucciones:")
print("    - Para cerrar el programa: presioná la tecla 'q', 'ESC' o el botón 'X' de la ventana.")
print("    - Para hacer pinch (agarrar): juntá el pulgar con el índice.")

cv2.namedWindow(WINDOW_NAME, cv2.WINDOW_AUTOSIZE)

prev_time = time.time()
start_time = time.time()
last_ts_ms = -1
fps = 0.0

# Variables de filtrado ultra-rápido (sin delay perceptible)
smooth_x, smooth_y, smooth_z = 0.5, 0.5, 0.0
smooth_fingers = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
filter_initialized = False
peace_persistence = 0

def smooth_val(prev, target, alpha, deadzone=0.0015):
    if abs(target - prev) < deadzone:
        return prev
    return prev + alpha * (target - prev)

running = True

def handle_sigint(sig, frame):
    global running
    running = False

signal.signal(signal.SIGINT, handle_sigint)

try:
    while running and cap.isOpened():
        success, image = cap.read()
        if not success or image is None:
            time.sleep(0.002)
            continue

        # Efecto espejo horizontal
        image = cv2.flip(image, 1)
        rgb_image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
        
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_image)
        ts_ms = int((time.time() - start_time) * 1000)
        if ts_ms <= last_ts_ms:
            ts_ms = last_ts_ms + 1
        last_ts_ms = ts_ms
        detection_result = detector.detect_for_video(mp_image, ts_ms)

        h, w, c = image.shape
        curr_time = time.time()
        dt = curr_time - prev_time
        if dt > 0.001:
            fps = 0.9 * fps + 0.1 * (1.0 / dt)
        prev_time = curr_time

        is_pinching_any = False
        is_peace_sign = False
        is_thumbs_up = False
        is_open_hand = False
        is_fist = False
        hands_found = 0

        if detection_result.hand_landmarks:
            hands_found = len(detection_result.hand_landmarks)
            # Usar únicamente la mano principal para evitar saltos entre dos detecciones
            hand_landmarks = detection_result.hand_landmarks[0]

            # Dibujar los 21 puntos
            for landmark in hand_landmarks:
                lx = int(landmark.x * w)
                ly = int(landmark.y * h)
                cv2.circle(image, (lx, ly), 4, (0, 255, 255), -1)

            thumb_tip = hand_landmarks[4]
            index_tip = hand_landmarks[8]
            tx, ty = int(thumb_tip.x * w), int(thumb_tip.y * h)
            ix, iy = int(index_tip.x * w), int(index_tip.y * h)

            cv2.line(image, (tx, ty), (ix, iy), (255, 200, 0), 2)

            distance = calculate_distance(thumb_tip, index_tip)
            wrist = hand_landmarks[0]

            # Usamos el centro de la palma (promedio entre muñeca 0 y nudillo medio 9)
            middle_mcp = hand_landmarks[9]
            raw_x = 0.5 * (wrist.x + middle_mcp.x)
            raw_y = 0.5 * (wrist.y + middle_mcp.y)

            palm_size = max(
                0.05,
                calculate_distance(wrist, hand_landmarks[5]),
                calculate_distance(wrist, hand_landmarks[9]),
                calculate_distance(wrist, hand_landmarks[17])
            )
            # Calibración ergonómica para escritorio:
            # Brazo en reposo / cerca del pecho: palm_size ~ 0.08 a 0.10 -> profundidad hacia atrás
            # Brazo estirado naturalmente hacia adelante: palm_size ~ 0.15 a 0.18 -> avanza sin salirse del encuadre
            # Se elimina la necesidad de acercarse demasiado al lente de la cámara para avanzar
            depth_norm = (palm_size - 0.115) * 11.5
            depth_norm = max(-0.85, min(1.35, depth_norm))
            raw_z = -depth_norm

            # Calcular flexión independiente y de alta sensibilidad para cada dedo (0.0 = extendido, 1.0 = cerrado)
            # Medimos la distancia punta-nudillo normalizada contra la longitud real de cada dedo
            def independent_finger_curl(mcp_idx, pip_idx, dip_idx, tip_idx):
                p_mcp = hand_landmarks[mcp_idx]
                p_pip = hand_landmarks[pip_idx]
                p_dip = hand_landmarks[dip_idx]
                p_tip = hand_landmarks[tip_idx]
                
                # Longitud anatómica total sumando los 3 segmentos
                total_len = (calculate_distance(p_mcp, p_pip) + 
                             calculate_distance(p_pip, p_dip) + 
                             calculate_distance(p_dip, p_tip))
                if total_len < 0.01:
                    return 0.0
                
                # Distancia directa del nudillo a la punta
                dist_direct = calculate_distance(p_mcp, p_tip)
                ratio = dist_direct / total_len
                
                # ratio extendido ~ 0.92, cerrado ~ 0.30
                curl = 1.0 - ((ratio - 0.28) / 0.64)
                return max(0.0, min(1.0, curl))

            raw_index  = independent_finger_curl(5, 6, 7, 8)
            raw_middle = independent_finger_curl(9, 10, 11, 12)
            raw_ring   = independent_finger_curl(13, 14, 15, 16)
            raw_pinky  = independent_finger_curl(17, 18, 19, 20)

            # Pulgar: flexión y oposición hacia la base del meñique
            thumb_len = (calculate_distance(hand_landmarks[1], hand_landmarks[2]) + 
                         calculate_distance(hand_landmarks[2], hand_landmarks[3]) + 
                         calculate_distance(hand_landmarks[3], hand_landmarks[4]))
            dist_thumb_opp = calculate_distance(hand_landmarks[4], hand_landmarks[17])
            thumb_ratio = dist_thumb_opp / max(0.05, palm_size)
            raw_thumb = max(0.0, min(1.0, 1.0 - (thumb_ratio - 0.55) / 0.70))

            # Distancia de pinza entre punta de pulgar y punta de índice
            raw_pinch = max(0.0, min(1.0, 1.0 - (distance / max(0.05, palm_size) - 0.20) / 0.45))

            if not filter_initialized:
                smooth_x, smooth_y, smooth_z = raw_x, raw_y, raw_z
                smooth_fingers = [raw_thumb, raw_index, raw_middle, raw_ring, raw_pinky, raw_pinch]
                filter_initialized = True
            else:
                # Suavizado cinemático optimizado: respuesta ultra-rápida sin delay (84% reactividad directa)
                smooth_x = smooth_val(smooth_x, raw_x, alpha=0.84, deadzone=0.0006)
                smooth_y = smooth_val(smooth_y, raw_y, alpha=0.84, deadzone=0.0006)
                smooth_z = smooth_val(smooth_z, raw_z, alpha=0.78, deadzone=0.0008)

                raw_f_list = [raw_thumb, raw_index, raw_middle, raw_ring, raw_pinky, raw_pinch]
                for i in range(6):
                    smooth_fingers[i] = smooth_val(smooth_fingers[i], raw_f_list[i], alpha=0.85, deadzone=0.0010)

            # Detección del Símbolo de Paz / Victoria (índice y mayor extendidos, anular y meñique cerrados)
            f_thumb, f_index, f_middle, f_ring, f_pinky, _ = smooth_fingers
            peace_detected_raw = (f_index < 0.45 and f_middle < 0.45 and f_ring > 0.42 and f_pinky > 0.42)
            if peace_detected_raw:
                peace_persistence = min(12, peace_persistence + 3)
            else:
                peace_persistence = max(0, peace_persistence - 1)
            is_peace_sign = (peace_persistence >= 3)

            # Los 4 dedos (índice, mayor, anular, meñique) doblados en puño
            four_fingers_closed = (f_index > 0.58 and f_middle > 0.58 and f_ring > 0.58 and f_pinky > 0.58)

            # Pulgar apuntando claramente hacia arriba (en imagen OpenCV Y=0 es el borde superior)
            # El extremo del pulgar (punto 4) está por encima del nudillo del pulgar (punto 2) y del índice (punto 5)
            thumb_is_up = (hand_landmarks[4].y < hand_landmarks[2].y - 0.030 and 
                           hand_landmarks[4].y < hand_landmarks[5].y and 
                           f_thumb < 0.45)

            # GESTO 1: Pulgar para arriba (👍) -> Avanzar deliberadamente en eje Z
            is_thumbs_up = (not is_peace_sign and four_fingers_closed and thumb_is_up)

            # GESTO 2: Puño Cerrado (✊) -> Retroceder en eje Z (dedos cerrados y pulgar recogido, no arriba)
            is_fist = (not is_peace_sign and not is_thumbs_up and f_index > 0.65 and f_middle > 0.65 and f_ring > 0.65 and f_pinky > 0.65 and f_thumb > 0.45)

            # REPOSO: Si no es pulgar arriba, ni puño, ni paz -> QUIETO TOTAL (Cero movimiento en el lugar)

            # Enviar posición [x, y, z] y articulación de dedos filtrados a Unity
            osc_client.send_message("/hand/wrist", [ float(smooth_x), float(smooth_y), float(smooth_z) ])
            osc_client.send_message("/hand/fingers", [ float(v) for v in smooth_fingers ])
            osc_client.send_message("/hand/peace", 1 if is_peace_sign else 0)
            osc_client.send_message("/hand/thumb", 1 if is_thumbs_up else 0)
            osc_client.send_message("/hand/open", 1 if is_thumbs_up else 0) # Retrocompatibilidad
            osc_client.send_message("/hand/fist", 1 if is_fist else 0)

            # Detección de pinza (con histéresis para que no parpadee)
            if distance / palm_size < 0.36 or distance < 0.06:
                is_pinching_any = True
                cv2.circle(image, ((tx + ix) // 2, (ty + iy) // 2), 10, (0, 255, 0), -1)

            if is_pinching_any:
                osc_client.send_message("/hand/pinch", 1)
            else:
                osc_client.send_message("/hand/pinch", 0)
        else:
            if filter_initialized:
                osc_client.send_message("/hand/pinch", 0)
                osc_client.send_message("/hand/peace", 0)
                osc_client.send_message("/hand/thumb", 0)
                osc_client.send_message("/hand/open", 0)
                osc_client.send_message("/hand/fist", 0)

        # Dibujar HUD
        if is_peace_sign:
            status_color = (0, 255, 255) # Amarillo/Cyan
            status_text = "SIMBOLO DE PAZ (V) [ACCION / ESCAPE]"
        elif is_thumbs_up:
            status_color = (0, 255, 100) # Verde brillante
            status_text = "PULGAR ARRIBA (THUMB UP) -> AVANZAR (Z +)"
        elif is_fist:
            status_color = (0, 140, 255) # Naranja
            status_text = "PUNO CERRADO (FIST) -> RETROCEDER (Z -)"
        elif is_pinching_any:
            status_color = (255, 100, 255)
            status_text = "AGARRANDO (PINCH)"
        elif hands_found > 0:
            status_color = (0, 165, 255)
            status_text = "MANO EN REPOSO (QUIETO EN EL LUGAR)"
        else:
            status_color = (200, 200, 200)
            status_text = "BUSCANDO MANO..."

        cv2.putText(image, f"Estado: {status_text}", (20, 35), cv2.FONT_HERSHEY_SIMPLEX, 0.8, status_color, 2)
        cv2.putText(image, f"FPS: {fps:.1f} | Manos: {hands_found}", (20, 70), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 1)
        if hands_found > 0:
            cv2.putText(image, f"Mano X:{smooth_x:.2f} Y:{smooth_y:.2f} Profundidad(Z):{-smooth_z:.2f}", (20, 100), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (200, 255, 200), 1)
            if is_peace_sign:
                cv2.putText(image, "PAZ DETECTADA (V) -> Mantene 5s en el Ojo", (20, 130), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (0, 255, 255), 2)
        cv2.putText(image, f"Enviando a: {subnet_broadcast}:{args.port} y 127.0.0.1", (20, h - 20), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (180, 180, 180), 1)

        cv2.imshow(WINDOW_NAME, image)

        # 1. Comprobar si el usuario presionó la 'X' de la ventana para cerrarla
        if cv2.getWindowProperty(WINDOW_NAME, cv2.WND_PROP_VISIBLE) < 1:
            print("[*] Ventana cerrada por el usuario.")
            break

        # 2. Comprobar teclado ('q' o ESC)
        key = cv2.waitKey(1) & 0xFF
        if key == ord('q') or key == 27:
            print("[*] Tecla de salida presionada.")
            break

except KeyboardInterrupt:
    print("\n[*] Interrupción por teclado.")

finally:
    print("[*] Liberando cámara y cerrando ventanas...")
    if cap is not None:
        cap.release()
    cv2.destroyAllWindows()
    print("[OK] Programa finalizado de forma limpia.")
