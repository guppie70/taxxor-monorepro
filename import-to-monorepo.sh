#!/bin/bash

# =============================================================================
# Import production Editor and DocumentStore files into the monorepo
# =============================================================================

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICES_DIR="$(dirname "$SCRIPT_DIR")"
MONOREPO_DIR="$SCRIPT_DIR"

EDITOR_SOURCE="${SERVICES_DIR}/Editor"
DOCUMENTSTORE_SOURCE="${SERVICES_DIR}/DocumentStore"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== gRPC Migration: Importing to Monorepo ===${NC}"
echo ""

# Check if source directories exist (production)
if [ ! -d "$EDITOR_SOURCE" ]; then
    echo -e "${RED}Error: Editor source directory not found at $EDITOR_SOURCE${NC}"
    exit 1
fi

if [ ! -d "$DOCUMENTSTORE_SOURCE" ]; then
    echo -e "${RED}Error: DocumentStore source directory not found at $DOCUMENTSTORE_SOURCE${NC}"
    exit 1
fi

# Check if target directories exist within monorepo
if [ ! -d "$MONOREPO_DIR/Editor" ]; then
    echo -e "${RED}Error: Editor target directory not found at $MONOREPO_DIR/Editor${NC}"
    exit 1
fi

if [ ! -d "$MONOREPO_DIR/DocumentStore" ]; then
    echo -e "${RED}Error: DocumentStore target directory not found at $MONOREPO_DIR/DocumentStore${NC}"
    exit 1
fi

# Show what will happen
echo -e "${YELLOW}This will import files from production into the monorepo:${NC}"
echo ""
echo "  Source directories:"
echo "    - Editor: $EDITOR_SOURCE"
echo "    - DocumentStore: $DOCUMENTSTORE_SOURCE"
echo ""
echo "  Target directories (monorepo):"
echo "    - Editor: $MONOREPO_DIR/Editor"
echo "    - DocumentStore: $MONOREPO_DIR/DocumentStore"
echo ""
echo -e "${RED}WARNING: This will overwrite files in the monorepo!${NC}"
echo "Make sure you've committed or stashed any local changes first."
echo ""

# Require manual confirmation
read -p "Continue? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborting."
    exit 1
fi

# Define rsync options
# Using --checksum to only sync actually changed files
# Using --itemize-changes to show what changed
RSYNC_OPTIONS=(
    -av
    --checksum
    --itemize-changes
    --exclude='.git'
	--exclude='.gitmodules'
    --exclude='.git/'
    --exclude='.gitignore'
    --exclude='node_modules'
    --exclude='node_modules/'
    --exclude='.DS_Store'
    --exclude='*.user'
    --exclude='.vs/'
    --exclude='obj/'
    --exclude='bin/'
    --exclude='*.md'
    --exclude='import-to-monorepo.sh'
    --exclude='split-from-monorepo.sh'
)

# Sync Editor (from production to monorepo)
echo ""
echo -e "${YELLOW}Importing Editor files...${NC}"
echo -e "${BLUE}Changed files:${NC}"
rsync "${RSYNC_OPTIONS[@]}" "$EDITOR_SOURCE/" "$MONOREPO_DIR/Editor/" | grep "^>" || echo "  (no changes)"
echo -e "${GREEN}✓ Editor imported${NC}"

# Sync DocumentStore (from production to monorepo)
echo ""
echo -e "${YELLOW}Importing DocumentStore files...${NC}"
echo -e "${BLUE}Changed files:${NC}"
rsync "${RSYNC_OPTIONS[@]}" "$DOCUMENTSTORE_SOURCE/" "$MONOREPO_DIR/DocumentStore/" | grep "^>" || echo "  (no changes)"
echo -e "${GREEN}✓ DocumentStore imported${NC}"

# Build verification
echo ""
echo -e "${YELLOW}=== Verifying builds ===${NC}"
echo ""

BUILD_FAILED=0

# Build DocumentStore
echo -e "${BLUE}Building DocumentStore.sln...${NC}"
cd "$MONOREPO_DIR"
if dotnet build DocumentStore.sln --verbosity quiet; then
    echo -e "${GREEN}✓ DocumentStore build successful${NC}"
else
    echo -e "${RED}✗ DocumentStore build failed${NC}"
    BUILD_FAILED=1
fi

# Build Editor
echo ""
echo -e "${BLUE}Building Editor/TaxxorEditor.sln...${NC}"
if dotnet build Editor/TaxxorEditor.sln --verbosity quiet; then
    echo -e "${GREEN}✓ Editor build successful${NC}"
else
    echo -e "${RED}✗ Editor build failed${NC}"
    BUILD_FAILED=1
fi

# Summary
echo ""
if [ $BUILD_FAILED -eq 0 ]; then
    echo -e "${GREEN}=== Import Completed Successfully ===${NC}"
    echo ""
    echo "Production files have been imported to the monorepo and both projects build successfully."
    echo ""
    echo "You can now:"
    echo "  1. Review changes with: git status"
    echo "  2. Make your modifications"
    echo "  3. Push back to production with: bash ./split-from-monorepo.sh"
else
    echo -e "${RED}=== Import Completed with Build Errors ===${NC}"
    echo ""
    echo "Production files have been imported, but one or more builds failed."
    echo "Please review the build errors above and fix them before proceeding."
    echo ""
    echo "To see detailed build output, run:"
    echo "  dotnet build DocumentStore.sln"
    echo "  dotnet build Editor/TaxxorEditor.sln"
    exit 1
fi
