#!/bin/bash

INSTALL_DIR="$HOME/.local/bin"
BINARY_URL=""
OS_TYPE=$(uname -s)
ARCH_TYPE=$(uname -m)

if [ "$OS_TYPE" == "Linux" ]; then
    BINARY_URL="https://github.com/saddexed/llogin2/releases/latest/download/llogin-linux-x64"
elif [ "$OS_TYPE" == "Darwin" ]; then
    if [ "$ARCH_TYPE" == "arm64" ]; then
        BINARY_URL="https://github.com/saddexed/llogin2/releases/latest/download/llogin-osx-arm64"
    else
        BINARY_URL="https://github.com/saddexed/llogin2/releases/latest/download/llogin-osx-x64"
    fi
fi

if [ -z "$BINARY_URL" ]; then
    echo "Unsupported OS or Architecture"
    exit 1
fi

echo "Installing llogin to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"

curl -L "$BINARY_URL" -o "$INSTALL_DIR/llogin"
chmod +x "$INSTALL_DIR/llogin"

echo ""
echo "Installation Successful!"
echo "Make sure $INSTALL_DIR is in your PATH."
