#!/bin/bash
set -euo pipefail

echo "=================================================="
echo "Installing global pnpm packages..."
echo "=================================================="

PNPM_HOME="${PNPM_HOME:-/home/vscode/.local/share/pnpm}"
mkdir -p "${PNPM_HOME}"

current_global_bin_dir="$(pnpm config get global-bin-dir || true)"
if [ "${current_global_bin_dir}" != "${PNPM_HOME}" ]; then
    pnpm config set global-bin-dir "${PNPM_HOME}"
fi

declare -a packages=(
    "@openai/codex:codex"
)

missing_packages=()
missing_bins=()
for entry in "${packages[@]}"; do
    package="${entry%%:*}"
    bin="${entry##*:}"
    if ! command -v "${bin}" >/dev/null 2>&1; then
        echo "  - missing: ${bin}"
        missing_bins+=("${bin}")
        missing_packages+=("${package}")
    else
        echo "  - present: ${bin}"
    fi
done

if [ "${#missing_packages[@]}" -gt 0 ]; then
    echo ""
    echo "Installing missing packages: ${missing_bins[*]}"
    pnpm add -g "${missing_packages[@]}"
    echo "Global packages installation complete"
else
    echo ""
    echo "All global packages already installed"
fi

echo "=================================================="
