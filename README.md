# UrlSupervisor – v6 (WPF .NET 8) – Sans notifications

✅ Compile & fonctionne sans WinForms/Toasts.  
Fonctionnalités :
- Thème clair/sombre (toggle)
- Groupes & Tags + filtres, groupement visuel par groupe
- Historique (vert=OK, rouge=KO), uptime, Start/Stop, Ping
- Édition/ajout/suppression, écriture XML & rechargement
- Export CSV des pannes
- Mode compact (densifie la vue)

## Démarrage
1) Ouvrir `UrlSupervisor.sln` (VS 2022+, Windows).  
2) F5.  
3) Modifier via **+** ou ✎, puis **Enregistrer**.

## XML
```xml
<Monitor name="..." url="https://..." intervalSeconds="10" order="1"
         enabled="true" timeoutSeconds="5" group="ClientA" tags="api,prod" />
```
