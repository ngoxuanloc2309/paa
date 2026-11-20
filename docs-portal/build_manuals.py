#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
SynaptiX Industrial Documentation Generator (Compatibility Entrypoint)
Redirects to the international print manual generator: docs-portal/build_user_manual_print.py
"""

import os
import sys
import subprocess

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PRINT_BUILDER = os.path.join(SCRIPT_DIR, "build_user_manual_print.py")

if __name__ == "__main__":
    res = subprocess.run([sys.executable, PRINT_BUILDER] + sys.argv[1:])
    sys.exit(res.returncode)
