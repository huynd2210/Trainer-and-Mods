import zipfile, os, sys

root = sys.argv[1]
out = sys.argv[2]
if os.path.exists(out):
    os.remove(out)
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    for dirpath, dirnames, filenames in os.walk(root):
        for f in sorted(filenames):
            full = os.path.join(dirpath, f)
            arc = os.path.relpath(full, root).replace(os.sep, '/')
            z.write(full, arc)
            print("added", arc, os.path.getsize(full))
print("->", out, os.path.getsize(out), "bytes")
