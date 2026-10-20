#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AlpFinans MCP Server - Root Launcher
===================================
"""
import os
import sys

TARGET_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "1_kisisel_finans_ve_vergi")
sys.path.insert(0, TARGET_DIR)
os.chdir(TARGET_DIR)

from mcp_server import main

if __name__ == "__main__":
    main()
