"""Vectores de bge-m3 para KnowledgeExplorer, sin red.

Protocolo por stdin/stdout, una línea por petición: un arreglo JSON de textos entra,
sale una línea base64 con n × 1024 float16 normalizados (similitud = producto punto).
Usa solo el modelo ya presente en la caché de Hugging Face; nunca descarga.
"""
import os
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["TRANSFORMERS_OFFLINE"] = "1"
import base64, json, sys
if sys.platform == "win32":  # prioridad baja: Revit y Enscape siempre ganan el procesador
    import ctypes
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)  # BELOW_NORMAL_PRIORITY_CLASS
import torch
from transformers import AutoModel, AutoTokenizer

MODEL = "BAAI/bge-m3"
# Convivir con Revit y Enscape: en CPU, la mitad de los núcleos; en GPU, fp16 y un tope de memoria
# para que un exceso falle aquí (y se reintente en CPU) en vez de quitarle VRAM a otro programa.
# ponytail: tope fijo de 2.5 GB de VRAM; subirlo solo si Enscape no está abierto.
torch.set_num_threads(max(1, (os.cpu_count() or 2) // 4))
device = "cpu"
if torch.cuda.is_available() and os.environ.get("LECG_EMBED_DEVICE") != "cpu":
    total = torch.cuda.get_device_properties(0).total_memory
    torch.cuda.set_per_process_memory_fraction(min(1.0, 2.5 * 2**30 / total))
    device = "cuda"
tok = AutoTokenizer.from_pretrained(MODEL, local_files_only=True)
model = AutoModel.from_pretrained(MODEL, local_files_only=True, torch_dtype=torch.float16 if device == "cuda" else torch.float32).to(device).eval()
out = sys.stdout.buffer
out.write(f"ready {device}\n".encode()); out.flush()

for line in sys.stdin.buffer:
    texts = json.loads(line)
    # Lotes por longitud: menos relleno, misma salida en el orden de entrada.
    order = sorted(range(len(texts)), key=lambda i: len(texts[i]))
    result = [None] * len(texts)
    with torch.inference_mode():
        for s in range(0, len(order), 16):
            idx = order[s:s + 16]
            def encode(group):
                batch = tok([texts[i] for i in group], padding=True, truncation=True, max_length=512, return_tensors="pt").to(device)
                return torch.nn.functional.normalize(model(**batch).last_hidden_state[:, 0].float(), dim=-1)
            try:
                cls = encode(idx)
            except torch.cuda.OutOfMemoryError:
                # Otro programa ocupa la GPU: libera y sigue de uno en uno; si tampoco cabe, el proceso termina y C# lo informa.
                torch.cuda.empty_cache()
                cls = torch.cat([encode([i]) for i in idx])
            for i, v in zip(idx, cls):
                result[i] = v.to(torch.float16).cpu().numpy().tobytes()
    out.write(base64.b64encode(b"".join(result)) + b"\n"); out.flush()
