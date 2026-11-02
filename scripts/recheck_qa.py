#!/usr/bin/env python3
"""Legacy validation entrypoint. The supported plaintext checks live in plaintext_qa.py.
This entrypoint delegates rather than testing removed encryption requirements.
"""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name('plaintext_qa.py')), run_name='__main__')
