# Plugins

Newtonsoft.Json 이외의 외부 DLL(ARCHITECTURE 4.2, T-37).

| DLL | 판 | 출처 | 쓰는 곳 |
|---|---|---|---|
| BouncyCastle.Cryptography.dll | 2.6.2 (`lib/netstandard2.0`) | NuGet `BouncyCastle.Cryptography`, `Directory.Packages.props`와 같은 판 | `Wizard.Client.asmdef`의 `precompiledReferences`(Ed25519 검증, ChaCha20-Poly1305) |

`isExplicitlyReferenced: 1`이라 asmdef가 이름으로 가리킬 때만 참조된다. 판을 올릴 때는 `Directory.Packages.props`와 이 DLL을 같이 바꾼다.
