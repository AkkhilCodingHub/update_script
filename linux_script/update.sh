#!/bin/bash

# Function to display messages in blue color
print_blue() {
    echo -e "\e[1;34m$1\e[0m"  # Display message in blue color
}

# Function to display messages in yellow color
print_yellow() {
    echo -e "\e[1;33m$1\e[0m"  # Display message in yellow color
}

# Function to display messages in green color
print_green() {
    echo -e "\e[1;32m$1\e[0m"  # Display message in green color
}

# Check if a command exists
command_exists() {
    command -v "$1" &> /dev/null
}

# Determine the sudo command to use
if command_exists sudo; then
    sudo="sudo"
elif command_exists doas && [ -f "/etc/doas.conf" ]; then
    sudo="doas"
else
    sudo="su -c"
fi

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
        $sudo ${package_manager} --noconfirm -S base-devel
        if [ ! -d ~/Github/yay-git ]; then
            mkdir -p ~/Github/yay-git
        fi
        cd ~/Github && git clone https://aur.archlinux.org/yay-git.git && $sudo chown -R $(whoami) ~/Github/yay-git
        cd ~/Github/yay-git && makepkg --noconfirm -si
    else
        echo "AUR helper already installed"
    fi
fi

# Install nala with apt-get if using nala package manager
if [ "$package_manager" == "apt-get" ]; then
    if ! command -v nala; then
        echo "Installing nala..."
        $sudo apt-get install nala -y
    fi
fi

# Update the system using the determined package manager
case $package_manager in
    yum)
        $sudo yum update -y
        ;;
    paru)
        $sudo paru -Syu --noconfirm
        ;;
    yay)
        $sudo yay -Syu --noconfirm
        ;;
    pacman)
        $sudo yay -Syu --noconfirm
        ;;
    emerge)
        $sudo emerge --sync
        ;;
    zypper)
        $sudo zypper refresh
        ;;
    nala)
        $sudo nala update && $sudo nala upgrade -y
        ;;
    apk)
        $sudo apk update
        ;;
    dnf)
        $sudo dnf update -y
        ;;
    nix-env)
        $sudo nix-env -u
        ;;
    rpm)
        $sudo rpm -Uvh
        ;;
    *)
        echo "Unsupported package manager."
        ;;
esac

# Check for dependencies and include jq
if ! command -v jq &> /dev/null; then
    print_yellow "⚠ jq is not installed. Installing jq..."
    if [ -x "$(command -v yum)" ]; then
        $sudo yum install jq -y
    elif [ -x "$(command -v pacman)" ]; then
        $sudo pacman -S jq --noconfirm
    elif [ -x "$(command -v apk)" ]; then
        $sudo apk add jq
    elif [ -x "$(command -v nala)" ]; then
        $sudo nala install jq -y
    else
        print_yellow "⚠ Unable to install jq. Please install jq manually."
        exit 1
    fi
fi

# Check for locate command and install if not present
if ! command -v locate &> /dev/null; then
    print_yellow "⚠ locate command is not installed. Installing locate..."
    case $package_manager in
        yum)
            $sudo yum install mlocate -y
            ;;
        pacman)
            $sudo pacman -S mlocate --noconfirm
            ;;
        apk)
            $sudo apk add mlocate
            ;;
        nala)
            $sudo nala install mlocate -y
            ;;
        *)
            print_yellow "⚠ Unable to install locate command automatically. Please install it manually."
            exit 1
            ;;
    esac
else
    print_blue "Perform updatedb for checking new repos"
    $sudo updatedb
fi


# Notify the start of updating Git repositories and pip packages
print_blue "▶ Starting the update process for Git repositories and pip packages..."

# Find all directories containing .git within the system
repos=$($sudo locate -r '/\.git$' | grep "^$HOME" | grep -Ev '/\.[^/]+/|\./' 2>/dev/null)

if [ -z "$repos" ]; then
    print_yellow "⚠ No git repositories found in the home directory."
else
    print_blue "▶ Starting to update repositories in the home directory..."
    while IFS= read -r repo; do
        repo_dir=$(dirname "$repo")
        print_blue "▶ Updating repository in $repo_dir..."

        {
            if [ -d "$repo_dir/.git" ]; then
                $sudo git -C "$repo_dir" fetch
                $sudo git -C "$repo_dir" pull
                print_green "✔ Repository in $repo_dir updated."
            else
                print_yellow "⚠ No .git directory found in $repo_dir. Skipping..."
            fi
        } || print_yellow "❌ Failed to update repository in $repo_dir. Skipping..."
    done <<< "$repos"
    print_blue "▶ All repositories in the home directory have been updated."
fi

# Check if flatpak is installed and update if available
if command -v flatpak &> /dev/null; then
    print_yellow "▶ Upgrading flatpak packages "
    flatpak update
fi

# Check if pipx is installed and upgrade packages through it if available
if command -v pipx &> /dev/null; then
    print_blue "▶ Upgrading packages using pipx..."
    pipx upgrade-all
elif command -v pip &> /dev/null; then
    # Search for the virtual environment directory and activate it
    VENV_PATH=$($sudo locate activate | grep '/bin/activate' | grep "^$HOME" | grep -Ev '/\.[^/]+/|\./' | head -n 1)
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
$sudo sync | $sudo tee /proc/sys/vm/drop_caches
print_green "✔ RAM cache cleared."

# Notify the completion of the update process
print_blue "▶ Update process completed."
