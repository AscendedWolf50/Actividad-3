using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class AuthHandler : MonoBehaviour
{
    private string Token;
    private string Username;
    private int currentScore = 0; 
    private string apiiUrl = "https://sid-restapi.onrender.com";

    [Header("UI References - Login/Register")]
    public GameObject panelLogin;
    public TMP_InputField usernameInputField;
    public TMP_InputField passwordInputField;
    public TMP_Text errorText; 

    [Header("UI References - Logged In")]
    public GameObject panelLogged;
    public TMP_Text usernameLabel;
    
    [Header("UI References - Leaderboard")]
    public GameObject panelLeaderboard;
    public TMP_Text leaderboardText; 

    private void Start()
    {
        Token = PlayerPrefs.GetString("Token", null);
        Username = PlayerPrefs.GetString("Username", null);
        
        panelLogged.SetActive(false);
        panelLeaderboard.SetActive(false);
        
        // Limpiamos cualquier error viejo al iniciar
        if(errorText != null) errorText.text = ""; 

        if (!string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(Username))
        {
            StartCoroutine(GetProfile());
        }
        else
        {
            Debug.Log("No hay token, se requiere iniciar sesión.");
            panelLogin.SetActive(true);
        }
    }

    public IEnumerator GetProfile()
    {
        UnityWebRequest www = UnityWebRequest.Get(apiiUrl + "/api/usuarios/" + Username);
        www.SetRequestHeader("x-token", Token);

        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error de sesión o token inválido: " + www.error);
            LogoutButtonHandler(); 
        }
        else
        {
            Debug.Log("Sesión válida.");
            
            User userProfile = JsonUtility.FromJson<User>(www.downloadHandler.text);
            currentScore = userProfile.data != null ? userProfile.data.score : 0;
            
            SetUIForUserLogged();
        }
    }

    public void LoginButtonHandler()
    {
        
        if (string.IsNullOrEmpty(usernameInputField.text) || string.IsNullOrEmpty(passwordInputField.text))
        {
            errorText.text = "Por favor, llena todos los campos.";
            return; 
        }

        errorText.text = "Cargando..."; 
        StartCoroutine(LoginCoroutine(usernameInputField.text, passwordInputField.text));
    }

    IEnumerator LoginCoroutine(string username, string password)
    {
        AuthData authData = new AuthData { username = username, password = password };
        string jsonData = JsonUtility.ToJson(authData);

        UnityWebRequest www = UnityWebRequest.Post(apiiUrl + "/api/auth/login", jsonData, "application/json");
        
        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            
            errorText.text = "Usuario o contraseña incorrectos.";
            Debug.LogError("Login fallido: Verifica credenciales. " + www.error);
        }
        else
        {
            AuthResponse authResponse = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);
            Token = authResponse.token;
            Username = authResponse.usuario.username;
            
            
            currentScore = authResponse.usuario.data != null ? authResponse.usuario.data.score : 0;

            PlayerPrefs.SetString("Token", Token);
            PlayerPrefs.SetString("Username", Username);

            errorText.text = ""; 
            SetUIForUserLogged();
        }        
    }

    public void RegisterButtonHandler()
    {
        
        if (string.IsNullOrEmpty(usernameInputField.text) || string.IsNullOrEmpty(passwordInputField.text))
        {
            errorText.text = "No dejes campos vacíos para registrarte.";
            return;
        }

        errorText.text = "Creando cuenta...";
        StartCoroutine(RegisterCoroutine(usernameInputField.text, passwordInputField.text));
    }

    IEnumerator RegisterCoroutine(string username, string password)
    {
        AuthData authData = new AuthData { username = username, password = password };
        string jsonData = JsonUtility.ToJson(authData);

        UnityWebRequest www = UnityWebRequest.Post(apiiUrl + "/api/usuarios", jsonData, "application/json");

        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success)
        {
            errorText.text = "El usuario ya existe o hubo un error de conexión.";
            Debug.LogError("Error al registrar: " + www.error);
        }
        else
        {
            errorText.text = "¡Registro exitoso! Iniciando sesión...";
            Debug.Log("Registro exitoso. Iniciando sesión automáticamente...");
            StartCoroutine(LoginCoroutine(username, password));
        }
    }

    public void LogoutButtonHandler()
    {
        PlayerPrefs.DeleteKey("Token");
        PlayerPrefs.DeleteKey("Username");
        Token = null;
        Username = null;
        currentScore = 0; 

        panelLogged.SetActive(false);
        panelLeaderboard.SetActive(false);
        panelLogin.SetActive(true);

        usernameInputField.text = "";
        passwordInputField.text = "";
        if(errorText != null) errorText.text = ""; 
    }

    public void AddScoreTest()
    {
        
        currentScore += 100;
        StartCoroutine(UpdateScoreCoroutine(currentScore)); 
    }

    IEnumerator UpdateScoreCoroutine(int scoreToUpdate)
    {
        UpdateData updateData = new UpdateData { 
            username = Username, 
            data = new UserGameData { score = scoreToUpdate } 
        };
        string jsonData = JsonUtility.ToJson(updateData);

        UnityWebRequest www = new UnityWebRequest(apiiUrl + "/api/usuarios", "PATCH");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("x-token", Token);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error actualizando score: " + www.error);
           
            currentScore -= 100; 
        }
        else
        {
            Debug.Log("¡Puntaje actualizado con éxito a: " + currentScore + "!");
            
            usernameLabel.text = "Bienvenido, " + Username + "\nPuntos: " + currentScore;
        }
    }

    public void ShowLeaderboard()
    {
        StartCoroutine(GetLeaderboardCoroutine());
    }

    IEnumerator GetLeaderboardCoroutine()
    {
        UnityWebRequest www = UnityWebRequest.Get(apiiUrl + "/api/usuarios?limit=10&sort=true");
        www.SetRequestHeader("x-token", Token);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error obteniendo leaderboard: " + www.error);
        }
        else
        {
            UsersResponse response = JsonUtility.FromJson<UsersResponse>(www.downloadHandler.text);
            
            panelLogged.SetActive(false);
            panelLeaderboard.SetActive(true);

            leaderboardText.text = "--- PLAYERS ---\n\n";
            foreach (User u in response.usuarios)
            {
                int playerScore = u.data != null ? u.data.score : 0;
                leaderboardText.text += $"{u.username} : {playerScore} pts\n";
            }
        }
    }

    public void CloseLeaderboard()
    {
        panelLeaderboard.SetActive(false);
        panelLogged.SetActive(true);
    }

    public void SetUIForUserLogged()
    {
        panelLogin.SetActive(false);
        panelLogged.SetActive(true);
     
        usernameLabel.text = "Bienvenido, " + Username + "\nPuntos: " + currentScore;
    }
}

//  JSON 
[System.Serializable]
public class AuthData { public string username; public string password; }

[System.Serializable]
public class UserGameData { public int score; }

[System.Serializable]
public class UpdateData { public string username; public UserGameData data; }

[System.Serializable]
public class User { public string _id; public string username; public UserGameData data; }

[System.Serializable]
public class AuthResponse { public User usuario; public string token; }

[System.Serializable]
public class UsersResponse { public List<User> usuarios; }