# show-tree.ps1
# This script prints a tree-like view of a directory and excludes folders by name.
# Usage example: .\show-tree.ps1 -Path . -MaxDepth 6 -ExcludeDirs obj,Debug,Release

param(
    [string]$Path = ".",
    [int]$MaxDepth = 100,
    [string[]]$ExcludeDirs = @(".git",".github",".vs","artifacts","assets","benchmark","build","docs","example","tests","tools","obj","Debug","Release")
)

function Show-Tree {
    param(
        [string]$Root,
        [int]$Depth = 0
    )

    if ($Depth -gt $script:MaxDepth) { return }

    # Get children, directories and files, sorted: directories first, then files
    $children = Get-ChildItem -LiteralPath $Root -Force |
                Sort-Object -Property @{Expression={$_.PSIsContainer};Descending=$true}, Name

    foreach ($child in $children) {
        # If it's a directory and matches an excluded name, skip it
        if ($child.PSIsContainer) {
            if ($script:ExcludeDirs -contains $child.Name) { continue }
            $indent = ('│   ' * $Depth) + '├── '
            Write-Output ("{0}{1}" -f $indent, $child.Name)
            Show-Tree -Root $child.FullName -Depth ($Depth + 1)
        } else {
            $indent = ('│   ' * $Depth) + '└── '
            Write-Output ("{0}{1}" -f $indent, $child.Name)
        }
    }
}

# expose script-level variables to function
$script:MaxDepth = $MaxDepth
$script:ExcludeDirs = $ExcludeDirs

# Resolve path and run
$resolved = Resolve-Path -LiteralPath $Path
Show-Tree -Root $resolved.ProviderPath -Depth 0