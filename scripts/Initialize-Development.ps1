# Generates local credentials only. Files remain ignored by Git.
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskPublicFile = Join-Path $taskRoot 'security.local.json'
$taskPrivateFile = Join-Path $taskRoot 'identity.private.json'
$taskEnvironmentFile = Join-Path $taskRoot '.env'
if ((Test-Path -LiteralPath $taskPublicFile) -or (Test-Path -LiteralPath $taskPrivateFile) -or (Test-Path -LiteralPath $taskEnvironmentFile)) {
    throw 'Local security files already exist. Preserve them; this script does not rotate credentials.'
}
$taskRsa = [System.Security.Cryptography.RSA]::Create(3072)
try {
    $taskPublic = [Convert]::ToBase64String($taskRsa.ExportSubjectPublicKeyInfo())
    $taskPrivate = [Convert]::ToBase64String($taskRsa.ExportPkcs8PrivateKey())
} finally { $taskRsa.Dispose() }
$taskOrderKey = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$taskCartKey = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$taskPaymentKey = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
@{ Auth = @{ PublicKey = $taskPublic; Issuer = 'ecommerce-identity'; Audience = 'ecommerce' };
   ServiceAuth = @{ Keys = @{ Order = $taskOrderKey; Cart = $taskCartKey; Payment = $taskPaymentKey } } } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskPublicFile -Encoding utf8
@{ Auth = @{ PrivateKey = $taskPrivate } } | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath $taskPrivateFile -Encoding utf8
@("AUTH_PUBLIC_KEY=$taskPublic", "AUTH_PRIVATE_KEY=$taskPrivate", "SERVICE_ORDER_KEY=$taskOrderKey",
  "SERVICE_CART_KEY=$taskCartKey", "SERVICE_PAYMENT_KEY=$taskPaymentKey") |
    Set-Content -LiteralPath $taskEnvironmentFile -Encoding utf8
Write-Output 'Local security configuration generated. No accounts were created.'
