#!/bin/bash
# Systematic Development Agents Installer
# Installs CLAUDE.md and specialized agents for Claude Code

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "🔧 Installing Systematic Development for Claude Code..."

# Create directories
mkdir -p ~/.claude/agents

# Install CLAUDE.md
cp "$SCRIPT_DIR/CLAUDE.md" ~/.claude/CLAUDE.md
echo "✅ Installed ~/.claude/CLAUDE.md"

# Install agents
for agent in planner researcher ui-expert ui-completeness verifier code-reviewer; do
    if [ -f "$SCRIPT_DIR/$agent.md" ]; then
        cp "$SCRIPT_DIR/$agent.md" ~/.claude/agents/
        echo "✅ Installed ~/.claude/agents/$agent.md"
    fi
done

echo ""
echo "🎉 Installation complete!"
echo ""
echo "Restart Claude Code to activate:"
echo "  /exit"
echo "  claude"
echo ""
echo "Verify with:"
echo "  /memory     - Check CLAUDE.md loaded"
echo "  /agents     - List available agents"
echo ""
echo "Available agents:"
echo "  @planner          - Task decomposition"
echo "  @researcher       - API/library verification"
echo "  @ui-expert        - Clean UI design guidance"
echo "  @ui-completeness  - Frontend coverage audit"
echo "  @verifier         - Implementation verification"
echo "  @code-reviewer    - Quality and fallback detection"
