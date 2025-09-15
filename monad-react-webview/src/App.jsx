import React, { useState, useEffect } from 'react';
import { PrivyProvider, usePrivy } from '@privy-io/react-auth';
import { useCrossAppAccounts } from '@privy-io/react-auth';
import { sendToUnity, closeWindowAfterDelay, isOpenedFromUnity } from './unityBridge';

// Configuration Privy
const PRIVY_APP_ID = "cmek64iqd02lql70b9fl64lm9";
const MONAD_GAMES_ID = "cmd8euall0037le0my79qpz42";

function MonadLoginComponent() {
  const { ready, authenticated, user, login } = usePrivy();
  const { linkCrossAppAccount } = useCrossAppAccounts();
  const [status, setStatus] = useState('ready');
  const [error, setError] = useState('');
  const [walletInfo, setWalletInfo] = useState(null);
  const [needsUsername, setNeedsUsername] = useState(false);

  // Fonction pour vérifier l'username via l'API Monad Games ID
  const checkUsernameFromAPI = async (walletAddress) => {
    try {
      const response = await fetch(`https://chogtanks-nft-servers.onrender.com/api/check-username?wallet=${walletAddress}`);
      const data = await response.json();
      
      if (data.hasUsername && data.user && data.user.username) {
        return data.user.username;
      }
      return null;
    } catch (error) {
      console.error('[MONAD WEBVIEW] Error checking username:', error);
      return null;
    }
  };
  const [sentToUnity, setSentToUnity] = useState(false);

  // Vérifier si l'utilisateur a déjà un compte cross-app lié
  const crossAppAccount = user?.linkedAccounts?.find(
    account => account.type === 'cross_app' && 
               account.providerApp?.id === MONAD_GAMES_ID
  );

  useEffect(() => {
    console.log('[MONAD WEBVIEW] Component mounted');
    console.log('[MONAD WEBVIEW] Ready:', ready, 'Authenticated:', authenticated);
    console.log('[MONAD WEBVIEW] User:', user);
    console.log('[MONAD WEBVIEW] Cross App Account:', crossAppAccount);
  }, [ready, authenticated, user, crossAppAccount]);

  const handleLogin = async () => {
    try {
      setStatus('logging_in');
      setError('');
      
      console.log('[MONAD WEBVIEW] Starting login process...');
      
      // Étape 1: Login Privy si pas encore authentifié
      if (!authenticated) {
        console.log('[MONAD WEBVIEW] User not authenticated, logging in...');
        await login();
        return; // Le useEffect se déclenchera après l'auth
      }

      // Étape 2: Lier compte Cross App si pas encore fait
      if (!crossAppAccount) {
        console.log('[MONAD WEBVIEW] Linking Cross App account...');
        setStatus('linking_account');
        
        await linkCrossAppAccount({ 
          appId: MONAD_GAMES_ID 
        });
        
        // Attendre que le user soit mis à jour
        setTimeout(() => {
          window.location.reload();
        }, 1000);
        return;
      }

      // Étape 3: Récupérer wallet address
      console.log('[MONAD WEBVIEW] Getting wallet address...');
      setStatus('getting_wallet');
      
      const embeddedWallet = crossAppAccount.embeddedWallets?.[0];
      if (!embeddedWallet?.address) {
        throw new Error('No embedded wallet found in cross-app account');
      }

      const walletAddress = embeddedWallet.address;
      
      // Vérifier l'username via l'API Monad Games ID
      console.log('[MONAD WEBVIEW] 🔍 Checking username for wallet:', walletAddress);
      const apiUsername = await checkUsernameFromAPI(walletAddress);
      
      let username = crossAppAccount.username || apiUsername;
      
      if (!username) {
        console.log('[MONAD WEBVIEW] ❌ No username found, user needs to create one');
        setNeedsUsername(true);
        setWalletInfo({ address: walletAddress, username: null });
        setStatus('needs_username');
        return;
      }

      console.log('[MONAD WEBVIEW] ✅ Success! Wallet:', walletAddress, 'Username:', username);
      
      setWalletInfo({
        address: walletAddress,
        username: username
      });

      // Envoyer à Unity
      handleSendToUnity({
        success: true,
        walletAddress: walletAddress,
        username: username,
        userId: user.id
      });

      setStatus('success');

    } catch (err) {
      console.error('[MONAD WEBVIEW] ❌ Error:', err);
      setError(err.message || 'Unknown error occurred');
      setStatus('error');
    }
  };

  const handleSendToUnity = (data) => {
    console.log('[MONAD WEBVIEW] 📤 Sending to Unity:', data);
    setSentToUnity(true);
    
    // Utiliser notre bridge optimisé pour envoyer les données à Unity
    const success = sendToUnity(data);
    
    if (success && data.success) {
      // Planifier la fermeture de la fenêtre après un délai
      closeWindowAfterDelay(3000);
    }
  };
  
  // Vérifier si la fenêtre est ouverte depuis Unity
  useEffect(() => {
    const fromUnity = isOpenedFromUnity();
    console.log('[MONAD WEBVIEW] 🔍 Opened from Unity:', fromUnity);
    
    // Ajouter un écouteur d'événements pour les messages de Unity
    const handleMessage = (event) => {
      if (event.data && event.data.type === 'UNITY_READY') {
        console.log('[MONAD WEBVIEW] 📨 Received UNITY_READY message');
        // Si nous avons déjà des données à envoyer, les renvoyer
        if (walletInfo && status === 'success') {
          handleSendToUnity({
            success: true,
            walletAddress: walletInfo.address,
            username: walletInfo.username,
            userId: user?.id || ''
          });
        }
      }
    };
    
    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  }, [walletInfo, status]);

  // Auto-login si déjà authentifié avec cross-app
  useEffect(() => {
    if (ready && authenticated && crossAppAccount && status === 'ready') {
      handleLogin();
    }
    
    // Mise à jour de l'UI quand l'utilisateur est authentifié et a un compte cross-app
    if (ready && authenticated && crossAppAccount && status !== 'success') {
      const embeddedWallet = crossAppAccount.embeddedWallets?.[0];
      if (embeddedWallet?.address) {
        console.log('[MONAD WEBVIEW] 🔄 Updating UI with wallet info');
        
        // Fonction async pour vérifier l'username
        const updateWalletInfo = async () => {
          const apiUsername = await checkUsernameFromAPI(embeddedWallet.address);
          const finalUsername = crossAppAccount.username || apiUsername;
          
          if (!finalUsername) {
            setNeedsUsername(true);
            setWalletInfo({ address: embeddedWallet.address, username: null });
            setStatus('needs_username');
            return;
          }
          
          setWalletInfo({
            address: embeddedWallet.address,
            username: finalUsername
          });
          setStatus('success');
          
          // Envoyer automatiquement à Unity si pas déjà fait
          if (!sentToUnity) {
            handleSendToUnity({
              success: true,
              walletAddress: embeddedWallet.address,
              username: finalUsername,
              userId: user.id
            });
          }
        };
        
        updateWalletInfo();
      }
    }
  }, [ready, authenticated, crossAppAccount, status, user, sentToUnity]);

  const getStatusMessage = () => {
    switch (status) {
      case 'logging_in': return 'Connecting to Privy...';
      case 'linking_account': return 'Linking Monad Games ID...';
      case 'getting_wallet': return 'Getting wallet address...';
      case 'needs_username': return 'Username required - please create one';
      case 'success': return 'Success! Sending to Unity...';
      case 'error': return 'Error occurred';
      default: return 'Ready to connect';
    }
  };

  const isLoading = ['logging_in', 'linking_account', 'getting_wallet'].includes(status);

  return (
    <div className="container">
      <div className="logo">🎮 CHOGTANKS</div>
      
      {!ready && (
        <div className="loading">Loading Privy SDK...</div>
      )}
      
      {ready && (
        <>
          <button 
            onClick={handleLogin}
            disabled={!ready || isLoading || status === 'success'}
            style={{
              background: isLoading ? '#ccc' : status === 'success' ? '#4CAF50' : 'linear-gradient(45deg, #667eea, #764ba2)',
              color: 'white',
              border: 'none',
              padding: '15px 30px',
              borderRadius: '25px',
              fontSize: '16px',
              fontWeight: 'bold',
              cursor: isLoading || status === 'success' ? 'not-allowed' : 'pointer',
              width: '100%',
              marginBottom: '20px'
            }}
          >
            {status === 'success' ? '✅ Connected' : isLoading ? getStatusMessage() : 'Sign in with Monad Games ID'}
          </button>
          
          {status === 'needs_username' && walletInfo && (
        <div className="error">
          <strong>⚠️ Username Required</strong>
          <div className="wallet-info">
            <div><strong>Wallet:</strong> {walletInfo.address}</div>
          </div>
          <div style={{margin: '15px 0'}}>
            You need to create a username to continue playing CHOGTANKS.
          </div>
          <button 
            onClick={() => window.open('https://monadclip.fun', '_blank')}
            style={{
              background: '#A0055D',
              color: 'white',
              border: 'none',
              padding: '12px 24px',
              borderRadius: '8px',
              cursor: 'pointer',
              fontSize: '16px',
              fontWeight: 'bold'
            }}
          >
            Create Username
          </button>
          <div style={{fontSize: '12px', marginTop: '10px', color: '#666'}}>
            After creating your username, refresh this page to continue.
          </div>
        </div>
      )}

      {status === 'success' && walletInfo && walletInfo.username && (
        <>
          <div className="success">
            <strong>✅ Connected Successfully!</strong>
            <div className="wallet-info">
              <div><strong>Username:</strong> {walletInfo.username}</div>
              <div><strong>Wallet:</strong> {walletInfo.address}</div>
            </div>
            <div style={{fontSize: '14px', marginTop: '10px'}}>
              {sentToUnity ? 'Data sent to Unity!' : 'Sending to Unity...'}
            </div>
          </div>
        </>
      )}    
          {error && (
            <div className="error">
              <strong>❌ Error:</strong> {error}
              <button 
                onClick={() => {setError(''); setStatus('ready');}}
                style={{
                  background: '#e74c3c',
                  color: 'white',
                  border: 'none',
                  padding: '8px 16px',
                  borderRadius: '15px',
                  fontSize: '12px',
                  marginTop: '10px',
                  cursor: 'pointer'
                }}
              >
                Retry
              </button>
            </div>
          )}
          
          <div className="loading">{getStatusMessage()}</div>
        </>
      )}
    </div>
  );
}

function App() {
  return (
    <PrivyProvider 
      appId={PRIVY_APP_ID}
      config={{
        loginMethodsAndOrder: {
          primary: [`privy:${MONAD_GAMES_ID}`],
        },
        embeddedWallets: {
          createOnLogin: 'users-without-wallets'
        }
      }}
    >
      <div style={{ padding: '20px', fontFamily: 'Arial, sans-serif' }}>
        <h1>🎮 Monad Games ID</h1>
        <MonadLoginComponent />
      </div>
    </PrivyProvider>
  );
}

export default App;
