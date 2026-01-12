# Target platforms

| Official name           | Technical name             | Variant | Architecture | Memory | Storage        | Hint                  |
|:------------------------|----------------------------|---------|:------------:|-------:|:---------------|-----------------------|
| Bosch ctrlX CORE X2/X3  | COREX-C-X2/3               |         | linux-arm64  |    2GB | 4GB            | Snap package          |
| Bosch ctrlX COREplus X3 | COREX-M-X3                 |         | linux-arm64  |    2GB | 4GB            | Snap package          |
| Bosch ctrlX COREplus X5 | COREX-M-X5                 |         | linux-arm64  |    8GB | 16GB           | Snap package          |
| Bosch ctrlX COREplus X7 | COREX-M-X7                 |         | linux-arm64  |   16GB | 32GB           | Snap package          |
| edgeGateway Size S      | VHIP4                      | Chimera | linux-arm64  |    1GB | 1,7GB incl. OS | very low power        |
| edgeGateway Size M      | VHIP6                      | Perseus | linux-arm64  |    4GB | 20GB+          |                       |
| ├                       | VHIP6                      | Hermes  | linux-arm64  |    4GB | 20GB+          |                       |
| └                       | VHIP6                      | PDM3    | linux-arm64  |    4GB | 20GB+          |                       |
| edgeGateway Size L      | VHIP7/8                    | ?       |      ?       |      ? | in planning    | in planning for AI/ML |
| ┬                       | Raspberry 4 Compute Module | EC10    | linux-arm64  |   4GB+ | 20GB+          |                       |
| └                       | Raspberry 4 Compute Module | EC20    | linux-arm64  |   4GB+ | 20GB+          |                       |
| IPC                     |                            |         |  linux-x64   |   1GB+ | 20GB+          |                       |
| Cloud                   |                            |         |  linux-x64   |   1GB+ | 20GB+          |                       |

## Performance ladder

| Performance | Similar hardware | ViciOne Suite | moneo                   | BOSCH                               |
|:-----------:|:-----------------|:--------------|:------------------------|:------------------------------------|
|     👍    | Intel i7         |               |                         | ctrlX COREplus X7                   |
|      ⬆     | Intel i5         |               | edgeGateway Size L, IPC |                                     |
|      ↕     | Raspberry Pi 5   |               |                         | ctrlX COREplus X5                   |
|      ↕     | Raspberry Pi 4   | EC10, EC20    |                         |                                     |
|      ⬇     | Raspberry Pi 3   |               | edgeGateway Size M      | ctrlX CORE X2/X3, ctrlX COREplus X3 |
|     👎    |                  |               | edgeGateway Size S      |                                     |


## Edge-S Setup

On edge-s all logs go to journald and can be viewed by `sudo journalctl -b -u vicione-suite`. Configuration can be determined by using `cat /etc/vicione-suite/conf.d/*`. The default suite installation is organized as:

| Path | Source |
|:-----|:------:|
| /opt/vicione-suite/ | Core.OS |
| /mnt/persistent/vicione-suite/AppData | Core.OS | 
| /var/opt/vicione-suite/Modules | Modules |
| /var/opt/vicione-suite/FunctionBlocks | ClusterManagement |
| /var/opt/vicione-suite/EngineHosts | ClusterManagement |
| /var/opt/vicione-suite/Iodds | ClusterManagement |
