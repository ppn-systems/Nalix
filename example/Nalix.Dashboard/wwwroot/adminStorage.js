window.adminStorage = {
    getLocal:     function(key)        { try { return localStorage.getItem(key);    } catch(e) { return null; } },
    setLocal:     function(key, value) { try { localStorage.setItem(key, value);    } catch(e) {} },
    removeLocal:  function(key)        { try { localStorage.removeItem(key);        } catch(e) {} },
    getSession:   function(key)        { try { return sessionStorage.getItem(key);  } catch(e) { return null; } },
    setSession:   function(key, value) { try { sessionStorage.setItem(key, value);  } catch(e) {} },
    removeSession:function(key)        { try { sessionStorage.removeItem(key);      } catch(e) {} },
    clearSession: function()           { try { sessionStorage.clear();              } catch(e) {} }
};
