#!/bin/bash

distro_name=$(cat /etc/os-release | grep PRETTY_NAME | cut -d'"' -f2)
current_user=$(whoami)
echo -e "\e[1;34m▶ Starting the system update for distro $distro_name \e[0m"

package_manager=""
if [ -f /etc/redhat-release ]; then
    package_manager="yum"
elif [ -f /etc/arch-release ]; then
    if [ -x "$(command -v paru)" ]; then
        package_manager="paru"
    elif [ -x "$(command -v yay)" ]; then
        package_manager="yay"
    else
        package_manager="pacman"
    fi
elif [ -f /etc/gentoo-release ]; then
    package_manager="emerge"
elif [ -f /etc/SuSE-release ]; then
    package_manager="zypper"
elif [ -f /etc/debian_version ]; then
    package_manager="nala"
elif [ -f /etc/alpine-release ]; then
    package_manager="apk"
elif [ -f /etc/fedora-release ]; then
    package_manager="dnf"
elif [ -f /etc/redhat-release ]; then
    package_manager="rpm"
else
    echo "Unsupported package manager."
fi

if [ "$package_manager" == "pacman" ]; then
    if ! command -v yay && ! command -v paru; then
        echo "Installing yay as AUR helper..."
        sudo ${package_manager} --noconfirm -S base-devel
        cd /opt && sudo git clone https://aur.archlinux.org/yay-git.git && sudo chown -R $(whoami) ./yay-git
        cd yay-git && makepkg --noconfirm -si
    else
        echo "AUR helper already installed"
    fi
fi

# Implement the use of nala with apt-get if nala is not installed
if [ "$package_manager" == "apt-get" ]; then
    if ! command -v nala; then
        echo "Installing nala..."
        sudo apt-get install nala -y
    fi
fi

# After the installation process is complete, start the update process using the command for each package manager
case $package_manager in
    yum)
        yum update -y
        ;;
    paru)
        paru -Syu --noconfirm
        ;;
    yay)
        yay -Syu --noconfirm
    ;;
    pacman)
        yay -Syu --noconfirm
    ;;
    emerge)
        emerge --sync
        ;;
    zypper)
        zypper refresh
        ;;
    nala)
        nala update && nala upgrade -y
        ;;
    apk)
        apk update
        ;;
    dnf)
        dnf update -y
        ;;
    rpm)
        rpm -Uvh
        ;;
    *)
        echo "Unsupported package manager."
        ;;
esac
echo -e "\e[1;34m▶ Starting the update process for Git repositories and pip packages...\e[0m"

# Find all directories containing .git within the system
repos=$(find $HOME -type d -name ".git" -not -path "$HOME/.*" 2>/dev/null)
if [ -z "$repos" ]; then
  echo -e "\e[1;33m⚠ No git repositories found in the system.\e[0m"
else
  # Iterate through each repository and perform git fetch and git pull
  while IFS= read -r repo; do
    # Get the parent directory of the .git directory
    repo_dir=$(dirname "$repo")
    echo -e "\e[1;34m▶ Updating repository in $repo_dir...\e[0m"

    # Fetch and pull updates for the repository directory
    {
      # Fetch and pull updates
      if [ -d "$repo_dir/.git" ]; then
        git -C "$repo_dir" fetch
        git -C "$repo_dir" pull --rebase
        echo -e "\e[1;32m✔ Repository in $repo_dir updated.\e[0m"
      else
        echo -e "\e[1;33m⚠ No .git directory found in $repo_dir. Skipping...\e[0m"
      fi
    } || echo -e "\e[1;31m❌ Failed to update repository in $repo_dir. Skipping...\e[0m"
  done <<< "$repos"
  echo -e "\e[1;34m▶ All repositories in the system have been updated.\e[0m"
fi

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo -e "\e[1;31m⚠ jq is not installed. Please install jq to proceed.\e[0m"
    exit 1
fi

# Check if the virtual environment directory exists and activate it
VENV_PATH="$HOME/pip_env/bin/activate"
if [ -f "$VENV_PATH" ]; then
    echo -e "\e[1;34m▶ Activating the virtual environment...\e[0m"
    source "$VENV_PATH"
else
    echo -e "\e[1;31m⚠ The directory $VENV_PATH does not exist.\e[0m"
    exit 1
fi

# Get a list of all outdated pip packages
echo -e "\e[1;34m▶ Checking for outdated pip packages...\e[0m"
outdated_packages=$(pip list --outdated --format=json)

# Check if there are any outdated packages
if [ "$outdated_packages" == "[]" ]; then
  echo -e "\e[1;32m✔ All pip packages are up to date.\e[0m"
else
  # Update each outdated package
  echo -e "\e[1;34m▶ Updating outdated pip packages...\e[0m"
  echo "$outdated_packages" | jq -r '.[].name' | while read -r package_name; do
    echo -e "\e[1;34m▶ Updating $package_name...\e[0m"
    pip install --upgrade "$package_name"
  done

  echo -e "\e[1;32m✔ All pip packages have been updated.\e[0m"
fi

# Distrobox updates
echo -e "\e[1;34m▶ Performing Distrobox updates...\e[0m"
distrobox-upgrade --all > /dev/null
echo -e "\e[1;32m✔ Distrobox updates complete.\e[0m"

# Clear RAM cache
echo -e "\e[1;34m▶ Clearing RAM cache...\e[0m"
sudo chown -R $(whoami):$(id -gn)
sudo sync | sudo tee /proc/sys/vm/drop_caches
echo -e "\e[1;32m✔ RAM cache cleared.\e[0m"

echo -e "\e[1;34m▶ Update process completed.\e[0m"
