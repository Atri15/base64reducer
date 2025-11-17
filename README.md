### 📦 **base64reducer**  
*A lightweight command-line utility to optimize images and convert them to Base64 or binary data — with strict size limits.*

Shrinks images (JPEG/WebP) by intelligently reducing quality and dimensions until the output fits within your specified **Base64 length** or **binary size** (e.g., for JSON payloads, data URIs, or API constraints). Supports files and `stdin` — perfect for build pipelines, web assets, and embedded resources.

✨ **Features**  
• Size-constrained output (`--max-bytes`, `--max-base64`)  
• Iterative quality reduction (90 → 30) + fallback resizing  
• stdin/stdout support (`-` for piping)  
• Outputs: Base64, data URI, or raw binary  
• Zero runtime dependencies (self-contained .NET)

```bash
cat image.jpg | base64reducer - --max-bytes 24576 --format WebP


```

Built with `ImageSharp` and `System.CommandLine`.  
🚀 Fast • 🔒 Lossy-optimized • 🐳 Container-friendly

--- 



## ▶️ Usage Examples

### 🖼️ **Basic Use Cases**

#### 1. Convert image to Base64 (no size limit)
```bash
base64reducer photo.jpg
# Output: UklGRtYLA... (full Base64 string)
```

#### 2. Limit binary size to **24 KB** (e.g. for JSON payloads)
```bash
base64reducer logo.png --max-bytes 24576
# Automatically reduces quality/size until ≤24,576 bytes
```

#### 3. Limit Base64 string to **32,768 chars** (≈24 KB binary)
```bash
base64reducer banner.jpg --max-base64 32768
```

---

### 📤 **Output Formats**

#### 4. Output full `data:` URI (ready for HTML/CSS)
```bash
base64reducer icon.png --max-bytes 10000 --data-uri
# Output: image/png;base64,iVBORw0KG...
```

#### 5. Save optimized **WebP** as binary file (not Base64)
```bash
base64reducer input.jpg --max-bytes 15000 --binary --output output.webp
# Produces a valid, smaller WebP file
```

#### 6. Force **JPEG** output (for legacy browser support)
```bash
base64reducer modern.webp --max-bytes 20000 --format Jpeg --output legacy.jpg
```

---

### 🔄 **Piping & Automation (stdin/stdout)**

#### 7. Read from `stdin`, write Base64 to file
```bash
curl -s https://example.com/avatar.jpg | base64reducer - --max-bytes 8192 > avatar.b64
```

#### 8. Embed optimized image directly into JSON (Bash)
```bash
AVATAR_B64=$(cat avatar.jpg | base64reducer - --max-bytes 12000)
echo "{\"avatar\":\"data:image/jpeg;base64,$AVATAR_B64\"}" > config.json
```

#### 9. Convert & compress in CI/CD pipeline
```yaml
# GitHub Actions example
- name: Optimize favicon
  run: |
    curl -s https://design.example.com/favicon.png \
      | base64reducer - --max-bytes 4096 --format WebP --binary \
      > public/favicon.webp
```

---

### ⚙️ **Fine-Tuning Compression**

#### 10. Aggressive compression: max 400px, quality 40–80
```bash
base64reducer huge-photo.jpg \
  --max-bytes 10000 \
  --max-size 400 \
  --quality 80 \
  --min-quality 40
```

#### 11. Preserve higher quality (min 70) — stricter size control
```bash
base64reducer product.jpg \
  --max-bytes 30000 \
  --min-quality 70 \
  --format WebP
# Fails if can't fit at quality ≥70
```

---

### 📊 **Diagnostics & Inspection**

#### 12. Check final size without saving
```bash
base64reducer image.jpg --max-bytes 16384 \
  | wc -c        # Linux/macOS: Base64 length
# or
base64reducer image.jpg --max-bytes 16384 --binary \
  | wc -c        # Binary size
```

#### 13. Compare JPEG vs WebP savings
```bash
# JPEG
base64reducer img.jpg --max-bytes 20000 --format Jpeg --binary | wc -c

# WebP (usually ~30% smaller)
base64reducer img.jpg --max-bytes 20000 --format WebP --binary | wc -c
```

---

### 🛠️ **Batch Processing (Bash/Zsh)**

#### 14. Optimize all PNGs in a folder to ≤12 KB WebP
```bash
mkdir -p optimized
for f in assets/*.png; do
  outfile="optimized/$(basename "$f" .png).webp"
  base64reducer "$f" --max-bytes 12288 --format WebP --binary --output "$outfile"
done
```

#### 15. One-liner: create thumbnail + data URI
```bash
base64reducer cover.jpg --max-size 200 --max-bytes 6144 --data-uri | tee thumb-uri.txt
```

---

### ❓ **Edge Cases & Debugging**

#### 16. What if it *can’t* compress enough?
```bash
base64reducer huge.tiff --max-bytes 1000
# ❌ Error: Failed to compress... Tried down to quality=30 and size=400px.
# → Pre-resize or increase limit
```

#### 17. Validate output is a valid image (after `--binary`)
```bash
base64reducer photo.jpg --max-bytes 10000 --binary --output test.webp
file test.webp           # Linux/macOS: "Web/P image data"
identify test.webp       # (if ImageMagick installed)
```

---

💡 **Pro Tips**
- Use `--max-bytes` instead of `--max-base64` — easier to reason about (e.g. “24 KB limit”).
- Prefer `--format WebP` unless you need JPEG for IE11 or older.
- Combine with `gzip` in HTTP — Base64 compresses well!
- For avatars/icons: `--max-size 256 --max-bytes 8192` is often perfect.

---
