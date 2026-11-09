# instalar-sentinela.ps1
# Instala o Sentinela em C:\Program Files\Sentinela e faz ele abrir com o Windows.
#
#   instalar-sentinela.bat              (como administrador)  -> instala ou atualiza
#   instalar-sentinela.bat -Remover     (como administrador)  -> desinstala
#
# Por que Program Files: o Sentinela roda como administrador. Numa pasta que so o
# administrador pode alterar, nenhum outro programa consegue trocar o .exe.

param([switch]$Remover)

$ErrorActionPreference = 'Stop'
$destino   = Join-Path $env:ProgramFiles 'Sentinela'
$exe       = Join-Path $destino 'Sentinela.exe'
$origem    = Join-Path $PSScriptRoot 'publicado\Sentinela.exe'
$atalho    = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Sentinela.lnk'
$tarefaLogon = 'Sentinela'
$tarefaAbrir = 'Sentinela-Abrir'
$usuario   = "$env:USERDOMAIN\$env:USERNAME"

function Escreva($t, $c = 'Gray') { Write-Host $t -ForegroundColor $c }

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { Escreva ' [ERRO] Rode como ADMINISTRADOR.' Red; exit 1 }

function Fechar-Sentinela {
    Get-Process Sentinela -ErrorAction SilentlyContinue | ForEach-Object {
        $_.Kill(); $_.WaitForExit(5000) | Out-Null
    }
}

Escreva ''
if ($Remover) {
    Escreva ' Removendo o Sentinela...' Cyan
    Fechar-Sentinela
    foreach ($t in $tarefaLogon, $tarefaAbrir) {
        if (Get-ScheduledTask -TaskName $t -ErrorAction SilentlyContinue) {
            Unregister-ScheduledTask -TaskName $t -Confirm:$false
            Escreva "  [OK] Tarefa $t removida" Green
        }
    }
    if (Test-Path $atalho) { Remove-Item $atalho -Force; Escreva '  [OK] Atalho do menu Iniciar removido' Green }
    if (Test-Path $destino) { Remove-Item $destino -Recurse -Force; Escreva "  [OK] Pasta $destino removida" Green }
    Escreva ''
    Escreva ' Pronto. O Windows Defender continua funcionando normalmente.' White
    exit 0
}

if (-not (Test-Path $origem)) {
    Escreva " [ERRO] Nao achei $origem" Red
    Escreva '        Compile antes com: dotnet publish (veja o LEIAME.md).' Red
    exit 1
}

Escreva ' Instalando o Sentinela...' Cyan

# 1. Copia o programa
Fechar-Sentinela
# Remove a versao antiga, que se chamava Painel do PC
foreach ($t in 'PainelPC', 'PainelPC-Abrir') {
    if (Get-ScheduledTask -TaskName $t -ErrorAction SilentlyContinue) { Unregister-ScheduledTask -TaskName $t -Confirm:$false }
}
Get-Process PainelPC -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); $_.WaitForExit(5000) | Out-Null }
$antigo = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Painel do PC.lnk'
if (Test-Path $antigo) { Remove-Item $antigo -Force }
$pastaAntiga = Join-Path $env:ProgramFiles 'PainelPC'
if (Test-Path $pastaAntiga) { Remove-Item $pastaAntiga -Recurse -Force }
New-Item -ItemType Directory -Force $destino | Out-Null
Copy-Item $origem $exe -Force
Escreva "  [OK] Programa copiado para $destino" Green

# 2. Tarefa que abre na bandeja quando voce entra no Windows (ja como admin, sem perguntar)
$config = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries `
            -DontStopIfGoingOnBatteries -MultipleInstances Parallel -StartWhenAvailable
$principal = New-ScheduledTaskPrincipal -UserId $usuario -LogonType Interactive -RunLevel Highest

$gatilho = New-ScheduledTaskTrigger -AtLogOn -User $usuario
$gatilho.Delay = 'PT20S'   # espera 20 s para nao pesar no inicio
Register-ScheduledTask -TaskName $tarefaLogon -Force -Settings $config -Principal $principal -Trigger $gatilho `
    -Action (New-ScheduledTaskAction -Execute $exe -Argument '--bandeja') `
    -Description 'Abre o Sentinela na bandeja ao entrar no Windows.' | Out-Null
Escreva '  [OK] Abre sozinho com o Windows (na bandeja, perto do relogio)' Green

# 3. Tarefa usada pelo atalho: abre a janela como admin sem o aviso do Windows
Register-ScheduledTask -TaskName $tarefaAbrir -Force -Settings $config -Principal $principal `
    -Action (New-ScheduledTaskAction -Execute $exe) `
    -Description 'Usada pelo atalho Sentinela do menu Iniciar.' | Out-Null

# 4. Atalho no menu Iniciar
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($atalho)
$lnk.TargetPath = Join-Path $env:SystemRoot 'System32\schtasks.exe'
$lnk.Arguments = "/run /tn $tarefaAbrir"
$lnk.IconLocation = "$exe,0"
$lnk.WindowStyle = 7   # minimizado: nao pisca janela preta
$lnk.Description = 'Sentinela'
$lnk.Save()
Escreva '  [OK] Atalho "Sentinela" no menu Iniciar' Green

# 5. Abre agora
Start-ScheduledTask -TaskName $tarefaAbrir
Escreva ''
Escreva ' Pronto! O Sentinela abriu. Ao fechar no X ele fica na bandeja.' White
Escreva ' Para sair de vez: clique com o botao direito no escudo > Sair.' White
