#!/bin/bash

# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Install PowerShell Core
if ! command -v pwsh &> /dev/null; then
    echo "Installing PowerShell Core..."
    sudo apt update
    sudo apt install -y wget apt-transport-https software-properties-common
    wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    sudo apt update
    sudo apt install -y powershell
    rm packages-microsoft-prod.deb
else
    echo "PowerShell Core is already installed."
fi

