import subprocess
import time

video_path = r"C:\Users\herna\Videos\Black lagon\01.mp4"

print("Test 1: Minimal arguments")
p1 = subprocess.Popen(['mpvnet', '--force-window=yes', video_path])
time.sleep(2)
print("Return code:", p1.poll())

print("Test 2: Full AniCS arguments (these cause mpvnet to crash or detach)")
p2 = subprocess.Popen([
    'mpvnet', 
    '--force-window=yes', 
    '--cache=yes', 
    '--demuxer-max-bytes=400M', 
    '--demuxer-readahead-secs=120', 
    '--demuxer-lavf-o=http_persistent=0',
    video_path
])
time.sleep(2)
print("Return code:", p2.poll())
