import os

def count_kb(path):
    total_bytes = 0
    for root, _, files in os.walk(path):
        for file in files:
            if file.endswith(".cs"):
                full_path = os.path.join(root, file)
                size = os.path.getsize(full_path)
                total_bytes += size

    print(f"Total size: {total_bytes / 1024:.2f} Kb")

if __name__ == "__main__":
    script_path = os.path.abspath(__file__)
    script_dir = os.path.dirname(script_path) 
    count_kb(script_dir)
