// DÉLÉGATION UNIQUE: toujours charger la version sécurisée
// (évite les 404 si Render lance par erreur le fichier racine)
try {
  module.exports = require('./chogtanks-servers-clean/signature-server.js');
} catch (e) {
  console.error('[BOOT] Impossible de charger chogtanks-servers-clean/signature-server.js:', e && e.message ? e.message : e);
  process.exit(1);
}


