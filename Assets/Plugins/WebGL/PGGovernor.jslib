mergeInto(LibraryManager.library, {
  PGSyncGovernorSettings: function () {
    try {
      if (typeof FS !== 'undefined' && typeof FS.syncfs === 'function') {
        FS.syncfs(false, function (error) {
          if (error) console.error('Governor settings could not be saved to browser storage.');
        });
      }
    } catch (_) {
      console.error('Governor settings could not be saved to browser storage.');
    }
  }
});
