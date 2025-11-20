#!/bin/bash

# =============================================================================
# Split monorepo changes back to Editor and DocumentStore
# =============================================================================

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICES_DIR="$(dirname "$SCRIPT_DIR")"
MONOREPO_DIR="$SCRIPT_DIR"

EDITOR_TARGET="${SERVICES_DIR}/Editor"
DOCUMENTSTORE_TARGET="${SERVICES_DIR}/DocumentStore"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== gRPC Migration: Splitting Monorepo ===${NC}"
echo ""

# Check if source directories exist within monorepo
if [ ! -d "$MONOREPO_DIR/Editor" ]; then
    echo -e "${RED}Error: Editor directory not found at $MONOREPO_DIR/Editor${NC}"
    exit 1
fi

if [ ! -d "$MONOREPO_DIR/DocumentStore" ]; then
    echo -e "${RED}Error: DocumentStore directory not found at $MONOREPO_DIR/DocumentStore${NC}"
    exit 1
fi

# Check if target directories exist
if [ ! -d "$EDITOR_TARGET" ]; then
    echo -e "${RED}Error: Editor target directory not found at $EDITOR_TARGET${NC}"
    exit 1
fi

if [ ! -d "$DOCUMENTSTORE_TARGET" ]; then
    echo -e "${RED}Error: DocumentStore target directory not found at $DOCUMENTSTORE_TARGET${NC}"
    exit 1
fi

# Confirm with user
echo -e "${YELLOW}This will sync changes from the monorepo to:${NC}"
echo "  - Editor: $EDITOR_TARGET"
echo "  - DocumentStore: $DOCUMENTSTORE_TARGET"
echo ""
echo -e "${RED}WARNING: This will overwrite files in your local repositories!${NC}"
echo "Make sure you've committed or stashed any local changes first."
echo ""
# Uncomment these lines to require manual confirmation:
# read -p "Continue? (y/n) " -n 1 -r
# echo
# if [[ ! $REPLY =~ ^[Yy]$ ]]; then
#     echo "Aborting."
#     exit 1
# fi

# Define rsync options
# Using --checksum to only sync actually changed files
# Using --itemize-changes to show what changed
RSYNC_OPTIONS=(
    -av
    --checksum
    --itemize-changes
    --exclude='.git'
    --exclude='.git/'
    --exclude='node_modules'
    --exclude='node_modules/'
    --exclude='.DS_Store'
    --exclude='*.user'
    --exclude='.vs/'
    --exclude='obj/'
    --exclude='bin/'
    --exclude='MIGRATION_PLAN.md'
    --exclude='split-from-monorepo.sh'
    --exclude='CLAUDE.md'
)

# Sync Editor
echo ""
echo -e "${YELLOW}Syncing Editor changes...${NC}"
echo -e "${BLUE}Changed files:${NC}"
rsync "${RSYNC_OPTIONS[@]}" "$MONOREPO_DIR/Editor/" "$EDITOR_TARGET/" | grep "^>" || echo "  (no changes)"
echo -e "${GREEN}✓ Editor synced${NC}"

# Sync DocumentStore
echo ""
echo -e "${YELLOW}Syncing DocumentStore changes...${NC}"
echo -e "${BLUE}Changed files:${NC}"
rsync "${RSYNC_OPTIONS[@]}" "$MONOREPO_DIR/DocumentStore/" "$DOCUMENTSTORE_TARGET/" | grep "^>" || echo "  (no changes)"
echo -e "${GREEN}✓ DocumentStore synced${NC}"

# Summary
echo ""
echo -e "${GREEN}=== Monorepo Split Successfully ===${NC}"
echo ""
echo "Changes have been synced to your local repositories."
echo ""
echo "Next steps:"
echo "  1. Test the changes locally by building both services:"
echo ""
echo "     cd $EDITOR_TARGET"
echo "     dotnet build TaxxorEditor.sln"
echo ""
echo "     cd $DOCUMENTSTORE_TARGET"
echo "     dotnet build DocumentStore.sln"
echo ""
echo "  2. Test in Docker environment if builds succeed:"
echo ""
echo "     cd /Users/jthijs/Documents/my_projects/taxxor/tdm"
echo "     npm start"
echo ""
echo "  3. If everything works, commit the changes:"
echo ""
echo "     cd $EDITOR_TARGET"
echo "     git add -A && git status"
echo "     git commit -m 'gRPC migration: [batch description]'"
echo "     git push"
echo ""
echo "     cd $DOCUMENTSTORE_TARGET"
echo "     git add -A && git status"
echo "     git commit -m 'gRPC migration: [batch description]'"
echo "     git push"
echo ""
echo "  4. To merge latest changes back to monorepo:"
echo "     Run the merge-to-monorepo.sh script from ../services/_grpc-migration-tools/"
echo ""
