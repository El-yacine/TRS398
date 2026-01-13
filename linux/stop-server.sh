#!/bin/bash
# Stop TRS-398 server if running

echo "Stopping TRS-398 server..."

# Find and kill processes
PIDS=$(pgrep -f "MyQC.WebAPI" 2>/dev/null)
if [ -n "$PIDS" ]; then
    echo "Found running server(s): $PIDS"
    kill $PIDS 2>/dev/null
    sleep 1
    
    # Force kill if still running
    PIDS=$(pgrep -f "MyQC.WebAPI" 2>/dev/null)
    if [ -n "$PIDS" ]; then
        echo "Force killing..."
        kill -9 $PIDS 2>/dev/null
    fi
    
    echo "✅ Server stopped"
else
    echo "No server process found"
fi

# Also free port 8000 if something is using it
PORT_PID=$(lsof -ti:8000 2>/dev/null)
if [ -n "$PORT_PID" ]; then
    echo "Freeing port 8000 (PID: $PORT_PID)..."
    kill $PORT_PID 2>/dev/null
    sleep 1
    echo "✅ Port 8000 freed"
fi

echo "Done."

