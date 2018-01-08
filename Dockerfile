FROM microsoft/windowsservercore

RUN powershell curl https://aka.ms/vs/15/release/vs_buildtools.exe -O vs_buildtools.exe
RUN powershell curl https://github.com/git-for-windows/git/releases/download/v2.15.1.windows.2/Git-2.15.1.2-64-bit.exe -O git-setup.exe
RUN powershell curl https://cmake.org/files/v3.10/cmake-3.10.0-win64-x64.msi -O cmake-setup.msi

RUN powershell Start-Process git-setup.exe -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES','/CLOSEAPPLICATIONS', '/DIR=C:\git' -Wait

RUN MKDIR C:\buildtools
RUN vs_buildtools.exe --layout C:\buildtools --lang en-US --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --passive --wait
RUN C:\buildtools\vs_setup.exe --noweb --passive --norestart --wait 

RUN msiexec /i cmake-setup.msi /quiet /norestart

RUN setx PATH "%ProgramFiles%\CMake\bin;%PATH%"

ARG grev=develop
ARG gfork=dotnet

RUN git clone https://github.com/%gfork%/corert
WORKDIR corert
RUN git fetch && git checkout %grev%
RUN build.cmd release && buildscripts\build-packages.cmd release

RUN Compress-Archive -Path bin\ -DestinationPath corert-releasebuild.zip
