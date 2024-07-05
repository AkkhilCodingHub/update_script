#!/bin/bash

# Function to display messages in blue color
print_blue() {
    echo -e "\e[1;34m$1\e[0m"
}

# Function to display messages in yellow color
print_yellow() {
    echo -e "\e[1;33m$1\e[0m"
}

# Function to display messages in green color
print_green() {
    echo -e "\e[1;32m$1\e[0m"
}

# Get the current distribution name and the current user
distro_name=$(cat /etc/os-release | grep PRETTY_NAME | cut -d'"' -f2)
current_user=$(whoami)

# Start the system update process with the distribution name
print_blue "▶ Starting the system update for distro $distro_name"

# Determine the package manager based on the distribution name
package_manager=""
case $distro_name in
    *"Red Hat"*)
        if [ -x "$(command -v yum)" ]; then
            package_manager="yum"
        elif [ -x "$(command -v rpm)" ]; then
            package_manager="rpm"
        fi
        ;;
    *"Arch"*)
        if [ -x "$(command -v paru)" ]; then
            package_manager="paru"
        elif [ -x "$(command -v yay)" ]; then
            package_manager="yay"
        else
            package_manager="pacman"
        fi
        ;;
    *"Gentoo"*)
        package_manager="emerge"
        ;;
    *"SUSE"*)
        package_manager="zypper"
        ;;
    *"Debian"*)
        package_manager="nala"
        ;;
    *"Alpine"*)
        package_manager="apk"
        ;;
    *"Fedora"*)
        package_manager="dnf"
        ;;
    *"NixOS"*)
        package_manager="nix-env"
        ;;
    *)
        echo "Unsupported package manager."
        ;;
esac

# Notify if the package manager is not supported

# Install yay as AUR helper if using pacman
if [ "$package_manager" == "pacman" ]; then
    if ! command -v yay && ! command -v paru; then
        echo "Installing yay as AUR helper..."
        sudo ${package_manager} --noconfirm -S base-devel
        if [ ! -d ~/Github/yay-git ]; then
            mkdir -p ~/Github/yay-git
        fi
        cd ~/Github && git clone https://aur.archlinux.org/yay-git.git && sudo chown -R $(whoami) ~/Github/yay-git
        cd ~/Github/yay-git && makepkg --noconfirm -si
    else
        echo "AUR helper already installed"
    fi
fi

# Install nala with apt-get if using nala package manager
if [ "$package_manager" == "apt-get" ]; then
    if ! command -v nala; then
        echo "Installing nala..."
        sudo apt-get install nala -y
    fi
fi


# Update the system using the determined package manager
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
    nix-env)
        nix-env -u
        ;;
    rpm)
        rpm -Uvh
        ;;
    *)
        echo "Unsupported package manager."
        ;;
esac

# Check for dependencies and include jq
if ! command -v jq &> /dev/null; then
    print_yellow "⚠ jq is not installed. Installing jq..."
    if [ -x "$(command -v apt-get)" ]; then
        sudo apt-get install jq -y
    elif [ -x "$(command -v yum)" ]; then
        sudo yum install jq -y
    elif [ -x "$(command -v pacman)" ]; then
        sudo pacman -S jq --noconfirm
    elif [ -x "$(command -v apk)" ]; then
        sudo apk add jq
    else
        print_yellow "⚠ Unable to install jq. Please install jq manually."
        exit 1
    fi
fi

# Check for locate command and install if not present
if ! command -v locate &> /dev/null; then
    case $package_manager in
        apt-get)
            print_yellow "⚠ locate command is not installed. Installing locate..."
            sudo apt-get install mlocate -y
            ;;
        yum)
            print_yellow "⚠ locate command is not installed. Installing locate..."
            sudo yum install mlocate -y
            ;;
        pacman)
            print_yellow "⚠ locate command is not installed. Installing locate..."
            sudo pacman -S mlocate --noconfirm
            ;;
        apk)
            print_yellow "⚠ locate command is not installed. Installing locate..."
            sudo apk add mlocate
            ;;
        *)
            print_yellow "⚠ locate command is not installed. Unable to install locate command automatically. Please install it manually."
            exit 1
            ;;
    esac

    # Perform updatedb after installing locate
    updatedb
fi

# Notify the start of updating Git repositories and pip packages
print_blue "▶ Starting the update process for Git repositories and pip packages..."

# Find all directories containing .git within the system
repos=$(locate -r '/\.git$' | grep "^$HOME" | grep -Ev '/\.[^/]+/|\./' 2>/dev/null)

if [ -z "$repos" ]; then
  echo -e "\e[1;33m⚠ No git repositories found in the home directory.\e[0m"
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
        git -C "$repo_dir" pull
        echo -e "\e[1;32m✔ Repository in $repo_dir updated.\e[0m"
      else
        echo -e "\e[1;33m⚠ No .git directory found in $repo_dir. Skipping...\e[0m"
      fi
    } || echo -e "\e[1;31m❌ Failed to update repository in $repo_dir. Skipping...\e[0m"
  done <<< "$repos"
  echo -e "\e[1;34m▶ All repositories in the home directory have been updated.\e[0m"
fi

# Check if pipx is installed and upgrade packages through it if available
if command -v pipx &> /dev/null; then
    print_blue "▶ Upgrading packages using pipx..."
    pipx upgrade-all
elif command -v pip &> /dev/null; then
    # Search for the virtual environment directory and activate it
    VENV_PATH=$(locate activate | grep '/bin/activate' | grep "^$HOME" | grep -Ev '/\.[^/]+/|\./' | head -n 1)
    if [ -n "$VENV_PATH" ]; then
        print_blue "▶ Activating the virtual environment..."
        source "$VENV_PATH"

        # Get a list of all outdated pip packages
        print_blue "▶ Checking for outdated pip packages..."
        outdated_packages=$(pip list --outdated --format=json)

        # Check if there are any outdated packages
        if [ "$outdated_packages" == "[]" ]; then
            print_green "✔ All pip packages are up to date."
        else
            # Update each outdated package
            print_blue "▶ Updating outdated pip packages..."
            echo "$outdated_packages" | jq -r '.[].name' | while read -r package_name; do
                print_blue "▶ Updating $package_name..."
                pip install --upgrade "$package_name"
            done

            print_green "✔ All pip packages have been updated."
        fi
    else
        print_yellow "⚠ No virtual environment found in the home directory."
        exit 1
    fi
else
    print_yellow "⚠ pip or pipx is not installed. Please install either to proceed."
    exit 1
fi

# Perform Distrobox updates
print_blue "▶ Performing Distrobox updates..."
distrobox-upgrade --all > /dev/null
print_green "✔ Distrobox updates complete."

# Clear RAM cache
print_blue "▶ Clearing RAM cache..."
sudo sync | sudo tee /proc/sys/vm/drop_caches
print_green "✔ RAM cache cleared."

# Notify the completion of the update process
print_blue "▶ Update process completed."

