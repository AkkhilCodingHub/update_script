#!/bin/bash

echo "Starting the system update using Paru..."
paru -Syu

echo "Starting the update process for Git repositories and pip packages..."

# Define the base directory to search for git repositories
BASE_DIR=~/github

# Check if the base directory exists
if [ ! -d "$BASE_DIR" ]; then
  echo "The directory $BASE_DIR does not exist."
  exit 1
fi

# Find all git repositories within the specified directory
echo "Searching for git repositories in $BASE_DIR..."
repos=$(find "$BASE_DIR" -type d -name ".git")

if [ -z "$repos" ]; then
  echo "No git repositories found in $BASE_DIR."
else
  # Iterate through each repository and perform git fetch and git pull
  for repo in $repos; do
    # Get the parent directory of the .git directory
    repo_dir=$(dirname "$repo")
    echo "Updating repository in $repo_dir..."

    # Navigate to the repository directory
    cd "$repo_dir" || {
      echo "Failed to navigate to $repo_dir. Skipping..."
      continue
    }

    # Fetch and pull updates
    git fetch
    git pull

    echo "Repository in $repo_dir updated."
  done

  echo "All repositories in $BASE_DIR have been updated."
fi

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "jq is not installed. Please install jq to proceed."
    exit 1
fi

# Check if the virtual environment directory exists and activate it
VENV_PATH="~/pip_env/bin/activate"
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
sudo sync && echo 3 | sudo tee /proc/sys/vm/drop_caches
echo "RAM cache cleared."

echo "Update process completed."

