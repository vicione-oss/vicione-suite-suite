
Zweck dieser Unittests ist es, die Datensynchronisation zwischen einer Quell- und einer Ziel-Datenbank zu simulieren.
Das Cluster-Management verwendet dieses Verfahren für die Synchronisation zwischen der Master-Datenbank und den Slave-Datenbanken.

Die Testfälle wurden für zwei Grundszenarien erstellt.

Im 1. Szenario sind sowohl die Quell- als auch die Ziel-Datenbank sqlite-Datenbanken. Hierbei muss nur das Tool "sqlite3" von der Homepage 
"https://www.sqlite.org/download.html" heruntergeladen und installiert werden.

Im 2. Szenario wird als Quell-Datenbank eine Postgres-Datenbank in einem Docker-Container verwendet. Diese simuliert die Master-Datenbank.
Die Installation des benötigten Docker-Containers sowie das Erstellen der Datenbanken wird nachfolgend ausführlich beschrieben
Als Ziel-Datenbank wird wiederum eine sqlite-Datenbank verwendet. Für das 2. Szenario wird vorausgesetzt, das Docker auf dem lokalen Rechner
installiert ist (z.B Docker-Desktop für Windows).

Die beschriebenen Installationsschritte gelten vorerst für Windows.

Szenario 1 (von Sqlite nach Sqlite)

Die verwendete Quell-Datenbanken wird durch den Testfall selbst erstellt oder muss für den entsprechenden Testfall 
durch Ausführen eines Scriptes erstellt werden:

	Testfall										Script - Kommandozeile						Script - Powershell
	
	FirstStep_CheckSimpleData						-
	
	SynchronizeSuccessful_EmployeeSmallDB			CreateDbEmployeeSmall.cmd					CreateDbEmployeeSmall.ps1
	
	SQLInjection_Insert_CancelInsert				CreateDbEmployeeInjectionCancelInsert.cmd	CreateDbEmployeeInjectionCancelInsert.ps1
	Fix_SQLInjection_Insert_CancelInsert
	
	SQLInjection_Insert_ExecuteSqlCommand			CreateDbEmployeeInjectionExecute.cmd		CreateDbEmployeeInjectionExecute.ps1
	Fix_SQLInjection_Insert_ExecuteSqlCommand
	

1. Eine Kommandozeile oder die Powershell/Terminal öffnen:

	- in das Verzeichnis "DbSqlite\SyncDataSource\" des Projektes "TestModule.Backend" wechseln

2. In der Kommandozeile/Powershell ein oben genanntes Script ausführen:
	
	- folgende Schritte werden in dem Sript abgearbeitet:
		* Im Zip-Archiv "SyncDataSourceDump.zip" befinden sich Testdaten zum Befüllen der Test-Datenbanken.
		  Das Archiv wird vom Nexus-Server "nexus.nsc-gmbh.de" in das lokale Verzeichnis heruntergeladen.
		  In einem Dialog-Fenster muss man sich dabei an dem Nexus-Server mit seinem Nutzer/Passwort anmelden.
		
		* Das Zip-Archiv wird entpackt. 
		
		* Die Quell-Datenbank mit den Tabellen und entsprechenden Daten erstellt.
	

Szenario 2 (von Postgres nach Sqlite)


1. Es ist ein Docker-Image für postgres zu installieren. Dazu wird in einer Kommandozeile folgender Befehl ausgeführt:

	docker pull postgres:alpine

2. Folgende Anpassung ist in der Datei "CreateDockerAndPostgresDBs.ps1" des Projektes "TestModule.Backend" vorzunehmen:

	- bei Variable "$ModuleDir" den absoluten Pfadnamen des Projektes "TestModule.Backend" eintragen/ändern
	
3. Eine Kommandozeile oder die Powershell/Terminal öffnen:

	- in das Verzeichnis "DbPostgres\SyncDataSource\" des Projektes "TestModule.Backend" wechseln

4. In der Kommandozeile das Script "CreateDockerAndPostgresDBs.cmd" ausführen:
	oder 
   In der Powershell das Script "CreateDockerAndPostgresDBs.ps1" ausführen:

	- folgende Schritte werden in dem Sript abgearbeitet:
		* Im Zip-Archiv "PostgresSyncDataSourceDump.zip" befinden sich Testdaten zum Befüllen der Test-Datenbanken.
		  Das Archiv wird vom Nexus-Server "nexus.nsc-gmbh.de" in das lokale Verzeichnis heruntergeladen.
		  In einem Dialog-Fenster muss man sich dabei an dem Nexus-Server mit seinem Nutzer/Passwort anmelden.
		
		* Das Zip-Archiv wird entpackt. 
		
		* Es wird das Verzeichnis "docker-entrypoint-initdb.d" erstellt. Dieses dient als Volume für den Docker-Container und enthält die Dump- und Script-Dateien.
		
		* Die dump-Dateien werden dieses Verzeichnis verschoben, die Script-Dateien dorthin kopiert.

		* Ein bereits angelgter Docker-Container "ViciOnePostgres" wird gelöscht.
		
		* Der Docker-Container "ViciOnePostgres" wird erstellt.

5. Die sich in dem Verzeichnis "docker-entrypoint-initdb.d" befindlichen Script-Dateien werden beim erstmaligen Start des Docker-Containers automatisch ausgeführt. 
   Sie erstellen die Test-Datenbanken und befüllen die Tabellen mit Daten.

6. Im Anschluss können die Unit-Tests der Testklasse "SynchronizeDataPostgres2SqliteFacts" ausgeführt werden.
