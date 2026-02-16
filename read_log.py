
try:
    with open('full_build.log', 'r', encoding='utf-16') as f:
        lines = f.readlines()
        for i, line in enumerate(lines):
            if "warning" in line.lower():
                print(f"Line {i}: {line.strip()}")
except Exception as e:
    print(e)
