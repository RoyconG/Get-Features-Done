#!/usr/bin/env bash
set -euo pipefail

# GFD Installer — symlinks repo into ~/.claude/ so Claude Code discovers it

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLAUDE_DIR="${HOME}/.claude"

echo "Installing GFD from: ${SCRIPT_DIR}"
echo "Target: ${CLAUDE_DIR}"
echo

# 1. Symlink get-features-done/ → repo root
#    This makes ~/.claude/get-features-done/bin/, templates/, workflows/, etc. available
TARGET="${CLAUDE_DIR}/get-features-done"
if [ -L "$TARGET" ]; then
    echo "Removing existing symlink: ${TARGET}"
    rm "$TARGET"
elif [ -d "$TARGET" ]; then
    echo "Backing up existing directory: ${TARGET} → ${TARGET}.bak"
    mv "$TARGET" "${TARGET}.bak"
fi
ln -s "$SCRIPT_DIR" "$TARGET"
echo "Linked: ${TARGET} → ${SCRIPT_DIR}"

# 2. Symlink commands/gfd/ → repo commands/
mkdir -p "${CLAUDE_DIR}/commands"
TARGET="${CLAUDE_DIR}/commands/gfd"
if [ -L "$TARGET" ]; then
    rm "$TARGET"
elif [ -d "$TARGET" ]; then
    mv "$TARGET" "${TARGET}.bak"
fi
ln -s "${SCRIPT_DIR}/commands" "$TARGET"
echo "Linked: ${TARGET} → ${SCRIPT_DIR}/commands"

# 3. Symlink each agent file
mkdir -p "${CLAUDE_DIR}/agents"
for agent in "${SCRIPT_DIR}/agents"/gfd-*.md; do
    basename="$(basename "$agent")"
    TARGET="${CLAUDE_DIR}/agents/${basename}"
    if [ -L "$TARGET" ]; then
        rm "$TARGET"
    elif [ -f "$TARGET" ]; then
        mv "$TARGET" "${TARGET}.bak"
    fi
    ln -s "$agent" "$TARGET"
    echo "Linked: ${TARGET} → ${agent}"
done

echo
echo "GFD installed successfully."
echo "Verify with: node ~/.claude/get-features-done/bin/gfd-tools.cjs --help"
