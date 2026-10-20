# 由 _gen_hash_vectors.py 生成；内容为 base64（纯 ASCII），避免非 ASCII 字面量被错误解码。
# 摘要由 .NET SHA256 计算，与 tools/contract-check 的 Python 实现无关。
function Get-ShaFromB64([string]$b64) {
    $bytes = [System.Convert]::FromBase64String($b64)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $hex = ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
    return "sha256:$hex"
}
$vectors = [ordered]@{}
$vectors['canonical-v1'] = 'eyJhIjpbdHJ1ZSxudWxsLCJ4Il0sImIiOjF9'
$vectors['canonical-v2'] = 'eyJzIjoiYVwiYlxcY1xuZFx0ZVx1MDAwMWYifQ=='
$vectors['canonical-v3'] = 'eyJ6IjoyLCLDqSI6MX0='
$vectors['canonical-v4'] = 'eyJhIjpbXSwiYmlnIjo5MDA3MTk5MjU0NzQwOTkxLCJuIjotMTIsIm8iOnt9fQ=='
$vectors['canonical-v5'] = 'eyJzbGFzaCI6ImEvYiIsInRleHQiOiLkuK3mlocifQ=='
$vectors['identity-catalog-id'] = 'eyJjYW5vbmljYWxfdmVyc2lvbiI6Im5ldC13aXJlLWNhbm9uLXYxIiwiZGVjbGFyYXRpb25zIjp7ImFjdGlvbl90eXBlcyI6W10sImNvbnRlbnRfdHlwZXMiOltdLCJlbnRpdHlfa2luZHMiOlt7ImlkIjoiZGVtbzpwbGF5ZXIiLCJzZW1hbnRpY192ZXJzaW9uIjoiMSJ9XSwiZmllbGRzIjpbXSwiZ3JvdXBfdHlwZXMiOltdLCJwaWxlX3R5cGVzIjpbXSwicmVsYXRpb25fdHlwZXMiOltdLCJzZXF1ZW5jZV90eXBlcyI6W10sInRlcm1pbmFsX3JlYXNvbnMiOltdfSwicHJvdG9jb2xfdmVyc2lvbiI6IjAuMiJ9'
$vectors['identity-request-describe'] = 'eyJjYW5vbmljYWxfdmVyc2lvbiI6Im5ldC13aXJlLWNhbm9uLXYxIiwib3BlcmF0aW9uIjoiZGVzY3JpYmUiLCJwYXlsb2FkIjp7fX0='
$vectors['identity-ruleset'] = 'eyJjYW5vbmljYWxfdmVyc2lvbiI6Im5ldC13aXJlLWNhbm9uLXYxIiwiZW5naW5lX3ZlcnNpb25zIjpbImNvcmUtMS4wIl0sImlyX3ZlcnNpb24iOiJpci0xIiwibnVtZXJpY19jb25maWciOnsicm91bmRpbmciOiJwZXItaG9vayJ9LCJwYWNrYWdlX2RpZ2VzdHMiOlsic2hhMjU2OmFhIl0sInBhY2thZ2Vfb3JkZXIiOlsiYmFzZS1zcGlyZSJdLCJwcm5nX3Byb2ZpbGUiOiJwcm5nLTEiLCJydWxlX2NvbmZpZyI6eyJkaWZmaWN1bHR5Ijoibm9ybWFsIn19'
$vectors['identity-backend-profile'] = 'eyJhY3Rpb25fbWFwcGluZ192ZXJzaW9uIjoiMSIsImJhY2tlbmRfa2luZCI6InNpbXVsYXRpb24iLCJjYW5vbmljYWxfdmVyc2lvbiI6Im5ldC13aXJlLWNhbm9uLXYxIiwiZmllbGRfYXZhaWxhYmlsaXR5Ijp7ImRlbW86aHAiOiJhdmFpbGFibGUifSwicHJvamVjdGlvbl9zZW1hbnRpY3NfdmVyc2lvbiI6IjEiLCJwdWJsaWNfaWRlbnRpdHlfdmVyc2lvbiI6IjEiLCJzdXBwb3J0ZWRfbWVjaGFuaXNtX2lkcyI6WyJkZW1vOmNoZWNrX2hwX3Zpc2libGUiXX0='
$vectors['identity-snapshot-storage'] = 'eyJhIjoieCIsInoiOlsxLHsiYSI6MiwiYiI6bnVsbH1dfQ=='
foreach ($key in $vectors.Keys) { Write-Output ('{0}`t{1}' -f $key, (Get-ShaFromB64 $vectors[$key])) }
