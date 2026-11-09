"""Compatibility entry point for rebuilding the cave ridge model in place."""
from pathlib import Path
import runpy

runpy.run_path(str(Path(__file__).with_name('rebuild_cave_ridge.py')), run_name='__main__')
