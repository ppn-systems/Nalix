#!/bin/bash

# Ensure Backend is executable
chmod +x ./Backend

echo "========================================="
echo " Setting up strict DDoS server.ini limit "
echo "========================================="
if [ -f "./server.ini" ]; then
    sudo mkdir -p /usr/share/Nalix/config
    sudo cp ./server.ini /usr/share/Nalix/config/server.ini
    echo "Copied strict server.ini to /usr/share/Nalix/config/"
else
    echo "Warning: server.ini not found alongside script."
fi

echo "========================================="
echo " Starting Nalix Backend (DDoS Test Mode) "
echo "========================================="

# Start backend in the background and redirect logs
./Backend > backend.log 2>&1 &
BACKEND_PID=$!

echo "Backend is running (PID: $BACKEND_PID)."
echo "Log file: backend.log"
echo "Metrics file: metrics.csv"
echo "Press [Ctrl+C] to stop the server and monitoring..."
echo "========================================="

# Start sar (sysstat) to capture detailed %user vs %system CPU data in the background
echo "Starting sysstat (sar) for detailed CPU profiling..."
sar -u 1 > sar_cpu.log &
SAR_PID=$!

# Prepare metrics CSV header
echo "Timestamp, CPU_Percent, RAM_MB, Temp_C" > metrics.csv

# Trap Ctrl+C to kill backend gracefully
trap 'echo ""; echo "Stopping Backend and sar..."; kill $BACKEND_PID 2>/dev/null; kill $SAR_PID 2>/dev/null; echo "Test finished. Download metrics.csv, backend.log, and sar_cpu.log to your PC."; exit' INT

# Monitoring loop (every 1 second)
while kill -0 $BACKEND_PID 2>/dev/null; do
    # Get CPU and RAM
    STATS=$(ps -p $BACKEND_PID -o %cpu,rss | tail -n 1)
    CPU=$(echo $STATS | awk '{print $1}')
    RAM_KB=$(echo $STATS | awk '{print $2}')
    
    if [ -z "$RAM_KB" ]; then
        RAM_KB=0
    fi
    RAM_MB=$((RAM_KB / 1024))
    
    # Get Temperature (works on Pi and most Linux)
    TEMP="N/A"
    if [ -f /sys/class/thermal/thermal_zone0/temp ]; then
        TEMP_RAW=$(cat /sys/class/thermal/thermal_zone0/temp)
        TEMP=$(awk -v t=$TEMP_RAW 'BEGIN {printf "%.1f", t/1000}')
    fi
    
    TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')
    echo "$TIMESTAMP, $CPU, $RAM_MB, $TEMP" >> metrics.csv
    
    # Print to console as well
    echo "[$TIMESTAMP] CPU: $CPU% | RAM: ${RAM_MB}MB | Temp: ${TEMP}C"
    
    sleep 1
done

echo "Backend process exited unexpectedly."
