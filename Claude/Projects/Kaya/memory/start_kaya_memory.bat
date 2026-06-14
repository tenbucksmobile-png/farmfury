@echo off
title Kaya Memory Proxy
cd /d %~dp0
echo Starting Kaya Memory Proxy on port 11435...
echo Point Home Assistant Ollama integration to host.docker.internal:11435
echo.
"C:\Users\Personel\AppData\Local\Programs\Python\Python313\python.exe" -m uvicorn kaya_ollama_proxy:app --host 0.0.0.0 --port 11435
pause
