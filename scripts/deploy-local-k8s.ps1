param(
    [ValidateSet("Up", "Down")]
    [string]$Action = "Up"
)

$ErrorActionPreference = "Stop"

$clusterName = "oficina-billing-local"
$namespace = "oficina-billing-local"
$image = "oficina-billing:local"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$manifest = Join-Path (Join-Path (Join-Path $repoRoot "k8s") "local") "billing.yaml"
$kubeConfig = if ($env:KIND_KUBECONFIG) {
    $env:KIND_KUBECONFIG
}
else {
    Join-Path (Join-Path $HOME ".kube") "config-kind-$clusterName"
}
$kubeConfigDirectory = Split-Path -Parent $kubeConfig

foreach ($tool in @("docker", "kind", "kubectl")) {
    if ($tool -eq "kind") {
        $kindCommand = Get-Command kind -ErrorAction SilentlyContinue
        if (-not $kindCommand -and $env:LOCALAPPDATA) {
            $kindPath = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Links\kind.exe"
            if (Test-Path $kindPath) {
                $kindCommand = $kindPath
            }
        }
        if (-not $kindCommand) {
            throw "Ferramenta obrigatoria nao encontrada no PATH: kind"
        }
    }
    elseif ($Action -eq "Up" -and -not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "Ferramenta obrigatoria nao encontrada no PATH: $tool"
    }
}

$kindExecutable = if ($kindCommand -is [string]) { $kindCommand } else { $kindCommand.Source }
$clusters = & $kindExecutable get clusters
if ($LASTEXITCODE -ne 0) {
    throw "Nao foi possivel consultar os clusters Kind."
}

if ($Action -eq "Down") {
    if ($clusters -notcontains $clusterName) {
        Write-Host "O cluster Kind '$clusterName' nao esta em execucao."
        exit 0
    }

    & $kindExecutable delete cluster --name $clusterName --kubeconfig $kubeConfig
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao remover o cluster Kind local."
    }

    Write-Host "Cluster Kind '$clusterName' removido; o Billing e os dados efemeros foram descartados."
    exit 0
}

if ($clusters -notcontains $clusterName) {
    New-Item -ItemType Directory -Path $kubeConfigDirectory -Force | Out-Null
    & $kindExecutable create cluster --name $clusterName --kubeconfig $kubeConfig --wait 180s
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao criar o cluster Kind local."
    }
}

if (-not (Test-Path $kubeConfig)) {
    throw "Kubeconfig local nao encontrado: $kubeConfig"
}

Push-Location $repoRoot
try {
    & docker build -t $image -f Dockerfile .
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao montar a imagem do Billing."
    }
}
finally {
    Pop-Location
}

& $kindExecutable load docker-image $image --name $clusterName
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao carregar a imagem do Billing no cluster local."
}

& kubectl --kubeconfig $kubeConfig create namespace $namespace --dry-run=client -o yaml |
    & kubectl --kubeconfig $kubeConfig apply -f -
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao criar o namespace local."
}

$existingSecret = & kubectl --kubeconfig $kubeConfig -n $namespace get secret billing-db-local --ignore-not-found -o name
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao consultar o Secret local do banco."
}

if (-not $existingSecret) {
    $randomBytes = New-Object byte[] 24
    $randomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $randomGenerator.GetBytes($randomBytes)
    }
    finally {
        $randomGenerator.Dispose()
    }

    $password = [Convert]::ToBase64String($randomBytes).TrimEnd("=").Replace("+", "x").Replace("/", "y")
    $connectionString = "Host=postgres;Port=5432;Database=oficina_billing;Username=billing_user;Password=$password"
    $secret = @{
        apiVersion = "v1"
        kind = "Secret"
        metadata = @{
            name = "billing-db-local"
            namespace = $namespace
        }
        type = "Opaque"
        stringData = @{
            "postgres-password" = $password
            "connection-string" = $connectionString
        }
    } | ConvertTo-Json -Depth 5

    $secret | & kubectl --kubeconfig $kubeConfig apply -f -
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao criar o Secret temporario do banco."
    }
}

& kubectl --kubeconfig $kubeConfig apply -f $manifest
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao aplicar os manifests locais."
}

& kubectl --kubeconfig $kubeConfig -n $namespace rollout status deployment/postgres --timeout=180s
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL nao ficou pronto no cluster."
}

& kubectl --kubeconfig $kubeConfig -n $namespace rollout status deployment/oficina-billing-service --timeout=240s
if ($LASTEXITCODE -ne 0) {
    & kubectl --kubeconfig $kubeConfig -n $namespace logs deployment/oficina-billing-service --tail=100
    throw "Billing nao ficou pronto. Consulte os logs acima."
}

Write-Host ""
Write-Host "Billing implantado no cluster Kind local."
Write-Host "Contexto isolado: $kubeConfig"
Write-Host "Para acessar a API, execute em outro terminal:"
Write-Host "kubectl --kubeconfig `"$kubeConfig`" -n $namespace port-forward service/oficina-billing-service 5001:80"
Write-Host "Depois teste: http://localhost:5001/health/ready e http://localhost:5001/swagger"
