#!/bin/bash

echo "Starting the system update using Paru..."
paru --noconfirm -Syu

echo "Starting the update process for Git repositories and pip packages..."

# Find all directories containing .git within the system
repos=$(find $HOME -type d -name ".git" -not -path "$HOME/.*" 2>/dev/null)
if [ -z "$repos" ]; then
  echo "No git repositories found in the system."
else
  # Iterate through each repository and perform git fetch and git pull
  while IFS= read -r repo; do
    # Get the parent directory of the .git directory
    repo_dir=$(dirname "$repo")
    echo "Updating repository in $repo_dir..."

    # Fetch and pull updates for the repository directory
    {
      # Fetch and pull updates
      if [ -d "$repo_dir/.git" ]; then
        git -C "$repo_dir" fetch
        git -C "$repo_dir" pull --rebase
        echo "Repository in $repo_dir updated."
      else
        echo "No .git directory found in $repo_dir. Skipping..."
      fi
    } || echo "Failed to update repository in $repo_dir. Skipping..."
  done <<< "$repos"
  echo "All repositories in the system have been updated."
fi

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "jq is not installed. Please install jq to proceed."
    exit 1
fi

# Check if the virtual environment directory exists and activate it
VENV_PATH="$HOME/pip_env/bin/activate"
if [ -f "$VENV_PATH" ]; then
    echo "Activating the virtual environment..."
    source "$VENV_PATH"
else
    echo "The directory $VENV_PATH does not exist."
    exit 1
fi

# Get a list of all outdated pip packages
echo "Checking for outdated pip packages..."
outdated_packages=$(pip list --outdated --format=json)

# Check if there are any outdated packages
if [ "$outdated_packages" == "[]" ]; then
  echo "All pip packages are up to date."
else
  # Update each outdated package
  echo "Updating outdated pip packages..."
  echo "$outdated_packages" | jq -r '.[].name' | while read -r package_name; do
    echo "Updating $package_name..."
    pip install --upgrade "$package_name"
  done

  echo "All pip packages have been updated."
fi

# Clear RAM cache
echo "Clearing RAM cache..."
sudo sync | sudo tee /proc/sys/vm/drop_caches
echo "RAM cache cleared."

echo "Update process completed."

