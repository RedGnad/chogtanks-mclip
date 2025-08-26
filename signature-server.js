const express = require('express');
const { ethers } = require('ethers');
const cors = require('cors');
require('dotenv').config();

const app = express();
app.use(express.json());
app.use(cors());

const port = process.env.PORT || 3001;

if (!process.env.GAME_SERVER_PRIVATE_KEY) {
    console.error('ERREUR: GAME_SERVER_PRIVATE_KEY non définie dans le fichier .env');
    process.exit(1);
}

const gameWallet = new ethers.Wallet(process.env.GAME_SERVER_PRIVATE_KEY);

console.log("Game Server Signer Address:", gameWallet.address);

function generateNonce() {
    return Date.now() + Math.floor(Math.random() * 1000000);
}

app.post('/api/mint-authorization', async (req, res) => {
    try {
        const { walletAddress, playerPoints } = req.body;
        
        if (!walletAddress || playerPoints === undefined) {
            return res.status(400).json({ error: "Missing parameters" });
        }

        const nonce = generateNonce();
        
        const messageHash = ethers.utils.solidityKeccak256(
            ['address', 'uint256', 'uint256', 'string'],
            [walletAddress, playerPoints, nonce, 'MINT']
        );
        
        const signature = await gameWallet.signMessage(ethers.utils.arrayify(messageHash));
        
        res.json({
            authorized: true,
            nonce: nonce,
            signature: signature,
            playerPoints: playerPoints
        });
        
    } catch (error) {
        console.error('Mint authorization error:', error);
        res.status(500).json({ error: "Internal server error" });
    }
});

app.post('/api/evolve-authorization', async (req, res) => {
    try {
        const { walletAddress, tokenId, targetLevel, playerPoints } = req.body;
        
        if (!walletAddress || !tokenId || !targetLevel || playerPoints === undefined) {
            return res.status(400).json({ error: "Missing parameters" });
        }

        const evolutionCosts = {
            2: 2,   
            3: 100, 
            4: 200, 5: 300, 6: 400,  
            7: 500, 8: 600, 9: 700, 10: 800
        };
        
        const requiredPoints = evolutionCosts[targetLevel];
        
        if (playerPoints < requiredPoints) {
            return res.json({
                authorized: false,
                error: `Insufficient points. Required: ${requiredPoints}, Available: ${playerPoints}`
            });
        }

        const nonce = generateNonce();
        
        const messageHash = ethers.utils.solidityKeccak256(
            ['address', 'uint256', 'uint256', 'uint256', 'uint256', 'string'],
            [walletAddress, tokenId, targetLevel, playerPoints, nonce, 'EVOLVE']
        );
        
        const signature = await gameWallet.signMessage(ethers.utils.arrayify(messageHash));
        
        res.json({
            authorized: true,
            nonce: nonce,
            signature: signature,
            evolutionCost: requiredPoints,
            targetLevel: targetLevel
        });
        
    } catch (error) {
        console.error('Evolve authorization error:', error);
        res.status(500).json({ error: "Internal server error" });
    }
});

app.post('/api/monad-games-id/update-player', async (req, res) => {
    try {
        const { playerAddress, scoreAmount, transactionAmount, actionType } = req.body;
        
        if (!playerAddress || scoreAmount === undefined || transactionAmount === undefined) {
            return res.status(400).json({ error: "Missing parameters" });
        }
        
        console.log(`[Monad Games ID] Received update request: Player=${playerAddress}, Score=${scoreAmount}, TxAmount=${transactionAmount}, Action=${actionType}`);
        
        const provider = new ethers.providers.JsonRpcProvider('https://testnet-rpc.monad.xyz/');
        const wallet = new ethers.Wallet(process.env.GAME_SERVER_PRIVATE_KEY, provider);
        
        const MONAD_GAMES_ID_CONTRACT = "0xceCBFF203C8B6044F52CE23D914A1bfD997541A4";
        const contractABI = [
            "function updatePlayerData(address player, uint256 scoreAmount, uint256 transactionAmount)"
        ];
        
        const contract = new ethers.Contract(MONAD_GAMES_ID_CONTRACT, contractABI, wallet);
        
        console.log(`[Monad Games ID] Calling updatePlayerData(${playerAddress}, ${scoreAmount}, ${transactionAmount})`);
        
        const tx = await contract.updatePlayerData(playerAddress, scoreAmount, transactionAmount);
        console.log(`[Monad Games ID] Transaction sent: ${tx.hash}`);
        
        const receipt = await tx.wait();
        console.log(`[Monad Games ID] Transaction confirmed in block ${receipt.blockNumber}`);
        
        res.json({ 
            success: true, 
            transactionHash: tx.hash, 
            blockNumber: receipt.blockNumber, 
            playerAddress, 
            scoreAmount, 
            transactionAmount, 
            actionType 
        });
        
    } catch (error) {
        console.error('[Monad Games ID] Error:', error);
        res.status(500).json({ 
            error: "Failed to submit to Monad Games ID", 
            details: error.message 
        });
    }
});

app.listen(port, () => {
    console.log(`Signature server running on port ${port}`);
    console.log(`Game Server Address: ${gameWallet.address}`);
});
