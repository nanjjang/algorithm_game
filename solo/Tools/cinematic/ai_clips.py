"""로컬 영상 AI(Wan 2.2 TI2V-5B, mlx-video)로 장면 클립을 만든다. Apple Silicon 맥 전용.

준비(한 번):
    python3.12 -m venv ~/ai-video/venv
    ~/ai-video/venv/bin/pip install "git+https://github.com/Blaizzy/mlx-video.git" "huggingface_hub[cli]" torch
    ~/ai-video/venv/bin/hf download Wan-AI/Wan2.2-TI2V-5B --local-dir ~/ai-video/Wan2.2-TI2V-5B   # 약 34GB

흐름:
    1) 각 영상 스크립트에 --plates 로 바탕 그림을 뽑는다(장면의 배경 + baked 그림, 자막·입자 없이):
       python3 solo/Tools/cinematic/disciple.py all --plates ~/ai-video/plates   (mirror/season/intro/story도 같다)
    2) 이 스크립트가 모델을 MLX로 한 번 바꾸고, 바탕 그림마다 클립을 만들어 clips/<이름>.mp4 로 둔다.
       ~/ai-video/venv/bin/python solo/Tools/cinematic/ai_clips.py [이름 ...] [--force] [--steps 30] [--frames 121]
    3) 영상 스크립트를 다시 돌리면 클립이 있는 장면은 클립을 배경으로 쓴다.
"""
import argparse
import glob
import os
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
CLIPS = os.path.join(HERE, "clips")
BASE = os.path.expanduser(os.environ.get("AI_VIDEO_HOME", "~/ai-video"))
CKPT = os.path.join(BASE, "Wan2.2-TI2V-5B")
MLX = os.path.join(BASE, "Wan2.2-TI2V-5B-MLX")
PLATES = os.path.join(BASE, "plates")
RAW = os.path.join(BASE, "raw")
NEG = ("blurry, low quality, distorted face, deformed hands, extra limbs, morphing, melting, flicker, "
       "camera shake, camera pan, zoom, text, watermark, photorealistic, 3d render")


def convert():
    if os.path.exists(os.path.join(MLX, "config.json")):
        return
    print("converting checkpoint to MLX …", flush=True)
    subprocess.run([sys.executable, "-m", "mlx_video.models.wan_2.convert", "--checkpoint-dir", CKPT,
                    "--output-dir", MLX, "--dtype", "bfloat16"], check=True)


def generate(name: str, steps: int, frames: int, seed: int, force: bool):
    out = os.path.join(CLIPS, name + ".mp4")
    if os.path.exists(out) and not force:
        print("skip", name, "(exists)"); return
    png, txt = os.path.join(PLATES, name + ".png"), os.path.join(PLATES, name + ".txt")
    prompt = open(txt).read().strip()
    os.makedirs(RAW, exist_ok=True)
    raw = os.path.join(RAW, f"{name}_s{seed}.mp4")
    t0 = time.time()
    subprocess.run([sys.executable, "-m", "mlx_video.models.wan_2.generate", "--model-dir", MLX,
                    "--image", png, "--prompt", prompt, "--negative-prompt", NEG,
                    "--width", "1280", "--height", "704", "--num-frames", str(frames),
                    "--steps", str(steps), "--seed", str(seed), "--output-path", raw], check=True)
    os.makedirs(CLIPS, exist_ok=True)
    # 게임에 넣는 쪽은 소리 없는 짧은 h264로 다시 싼다
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", raw, "-an", "-c:v", "libx264", "-crf", "16",
                    "-pix_fmt", "yuv420p", out], check=True)
    print(f"clip {name}: {time.time() - t0:.0f}s → {out}", flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*", help="만들 클립 이름(없으면 plates 폴더 전부)")
    ap.add_argument("--steps", type=int, default=30)
    ap.add_argument("--frames", type=int, default=121, help="4n+1. 121 = 24fps에서 약 5초")
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    convert()
    names = a.names or sorted(os.path.splitext(os.path.basename(p))[0] for p in glob.glob(os.path.join(PLATES, "*.png")))
    for n in names:
        generate(n, a.steps, a.frames, a.seed, a.force)


if __name__ == "__main__":
    main()
