#!/bin/sh
# Starts one run_pair at a time (refuses while another launcher or sandbox game is alive); waits for it.
cd "$(dirname "$0")/../.."
if tasklist | grep -qi "swamp keeper.exe"; then echo "A game process is still running; not starting"; exit 2; fi
python ModsSource/SwampKeeperCoop/run_pair.py "$@" > ModsSource/SwampKeeperCoop/last-run.txt 2>&1
echo "exit $?"; tail -4 ModsSource/SwampKeeperCoop/last-run.txt
