import subprocess

def run_build():
    try:
        process = subprocess.Popen(
            ['dotnet', 'build', 'c:\\LECG\\RevitAddins\\LECG\\LECG.csproj', '--no-incremental', '/clp:WarningsOnly'],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True
        )
        stdout, stderr = process.communicate()
        print("STDOUT:", stdout)
        print("STDERR:", stderr)
    except Exception as e:
        print(f"Error: {e}")

run_build()
