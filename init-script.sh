#!/bin/bash
# Install prerequisites
sudo apt-get update && sudo apt-get install -y wget apt-transport-https

# Add Microsoft package repository and install .NET 8 runtime
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-runtime-8.0

# Optionally, pull your application container/image or do additional configuration here

# Install EF Core tools
dotnet tool install --global dotnet-ef
