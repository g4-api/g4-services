# Module 4: Tokens

[⬅ Back to overview](README.md) · [⬅ Module 3](03-data-folder-and-volume.md)

Where you are: the Hub keeps its data. About 4 minutes.

Two facts to keep apart. The Hub has no sign-in of its own. The G4 engine inside it checks the G4 token that an automation carries.

## What you'll do

- See that the Hub does not authenticate callers
- Put the token in the right place in a request
- Know what the engine does with it

## Steps

1. Know what the Hub does not do. A search of the Hub source for `UseAuthentication`, `UseAuthorization` and `AddAuthentication` finds no match. Anyone who can reach the port can call the REST API, the MCP endpoint and the SignalR hubs. Protect the port with your network, a reverse proxy or a firewall.

2. Put the token in the automation body. Every invoke request carries an `authentication` object. The token goes in its `token` field:

   ```json
   {
       "authentication": {
           "token": "<your-g4-token>"
       },
       "driverParameters": {
           "driver": "ChromeDriver",
           "driverBinaries": "<driver-binaries-or-grid-url>"
       },
       "stages": []
   }
   ```

   This example only shows the position of the token. The full request is in the Hub README.

3. Know what the engine does. When the request has a non-empty `token`, or a license file exists, the engine connects in offline mode with that token. Without a token it needs a username and password, which it checks against `G4:LoginManagerUri`. Studio and the other G4 clients send the token for you from the project `manifest.json`.

4. Keep tokens out of your files. Do not commit a real token to a README, an `.http` file or a collection. Use a placeholder such as `<your-g4-token>` in anything you share.

## Check your work

- You can say why the Hub needs a firewall or proxy in front of it.
- You know the token belongs in `authentication.token` of the request body.

## Where the facts come from

| Fact | Source |
| --- | --- |
| No authentication middleware | A search of `src` for `UseAuthentication`, `UseAuthorization` and `AddAuthentication` finds no match |
| Token or license file selects offline login | `g4-engine\src\G4.Plugins\Utilities\LoginManager.cs:113-116` |
| Username and password go to the login service | `g4-engine\src\G4.Plugins\Utilities\LoginManager.cs:143` |
| Login service address and `/api/Account/FindUser` suffix | `src\G4.Settings\AppSettings.cs:71-86` |

Next: [Module 5: Verify the Hub](05-verify-the-hub.md)
