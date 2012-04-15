/*
 Copyright (c) 2012 [Joerg Ruedenauer]
 
 This file is part of Ares.

 Ares is free software; you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation; either version 2 of the License, or
 (at your option) any later version.

 Ares is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with Ares; if not, write to the Free Software
 Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
 */
package ares.controller.android;

import java.net.UnknownHostException;
import java.util.ArrayList;
import java.util.HashMap;

import android.content.Intent;
import android.preference.PreferenceManager;
import android.support.v4.app.Fragment;
import android.util.Log;
import android.widget.Toast;
import ares.controllers.control.Control;
import ares.controllers.network.IServerListener;
import ares.controllers.network.ServerInfo;
import ares.controllers.network.ServerSearch;

public abstract class ConnectedFragment extends Fragment implements IServerListener {

	private ServerSearch serverSearch = null;
	
	private static Boolean sOnXLargeScreen = null;
	
	protected boolean isOnXLargeScreen() {
		if (sOnXLargeScreen == null) {
			sOnXLargeScreen = (getActivity().findViewById(R.id.modeFragmentContainer) != null);
		}
		return sOnXLargeScreen;
	}
	
	protected boolean isControlFragment() {
		return false;
	}

	public void onStart() {
		super.onStart();

		if (serverSearch != null) {
			serverSearch.dispose();
		}
		serverSearch = new ServerSearch(this, getServerSearchPort());
        
		ConnectionManager.getInstance().addClient();
		
		if (Control.getInstance().getConfiguration() == null && !isOnXLargeScreen() && !isControlFragment()) {
			// no configuration loaded, not in control fragment, not in main activity
        	// switch to main activity so that control fragment is displayed
        	// and project can be opened
			Log.d("ConnectedFragment", "Switch to main activity because no project opened");
        	Intent intent = new Intent(getActivity().getBaseContext(), MainActivity.class);
        	startActivity(intent);    	
        	return;
		}
		
		boolean connected = Control.getInstance().isConnected();
        if (connected) {
        	// everything ok
        	// Log.d("ConnectedFragment", "Already connected");
        	connectWithFirstServer = false;
        }
        else if (isControlFragment() || !isOnXLargeScreen()){
        	// not connected, in own activity --> search for servers
        	servers.clear();
        	serverNames.clear();
        	connectWithFirstServer = true;
        	tryConnect();
        }
	}
	
	public void onStop() {
    	if (!Control.getInstance().isConnected()) {
    		Log.d("ConnectedFragment", "Stopping server search");
    		serverSearch.stopSearch();
    	}
		
		if (ConnectionManager.getInstance().removeClient() == 0) {
	    	if (Control.getInstance().isConnected()) {
				doDisconnect(false, true);
	    	}
		}
		super.onStop();
	}
	
	protected void onPrefsChanged() {
		if (!Control.getInstance().isConnected())
		{
    		Log.d("ConnectedFragment", "Stopping server search");
			serverSearch.stopSearch();
		}
		servers.clear();
		serverNames.clear();
		if (serverSearch != null) {
			serverSearch.dispose();
		}
		serverSearch = new ServerSearch(this, getServerSearchPort());
		if (!Control.getInstance().isConnected()) 
		{
			if (isControlFragment() || !isOnXLargeScreen()) {
				tryConnect();
			}
		}    			
	}
	
	protected void onConnect(ServerInfo info) {
	}
	
	protected void onDisconnect(boolean startServerSearch) {
		if (startServerSearch && (isControlFragment() || !isOnXLargeScreen())) {
			if (!serverSearch.isSearching()) {
	    		Log.d("ConnectedFragment", "Starting server search");
				serverSearch.startSearch();
			}
		}
	}
	
	protected void doDisconnect(boolean startServerSearch, boolean informServer) {
		Log.d("ConnectedFragment", "Disconnecting (" + startServerSearch + ", " + informServer + ")");
		Control.getInstance().disconnect(informServer);
		PlayingState.getInstance().clearState();
		onDisconnect(startServerSearch);
	}
	
	protected void doConnect(ServerInfo info) {
		Log.d("ConnectedFragment", "Stopping server search");
		serverSearch.stopSearch();
		Log.d("ConnectedFragment", "Connecting with server");
		Control.getInstance().connect(info, PlayingState.getInstance(), false);		
		onConnect(info);
	}
	
    protected void tryConnect() {
    	String connectMode = PreferenceManager.getDefaultSharedPreferences(getActivity().getBaseContext()).getString("player_connection", "auto");
    	if (connectMode.equals("auto")) {
    		if (!serverSearch.isSearching()) {
	    		Log.d("ConnectedFragment", "Starting server search");
	    		serverSearch.startSearch();
    		}
    	}
    	else {
    		try {
    			ServerInfo info = ServerSearch.getServerInfo(connectMode, ",");
    			if (info == null) {
    				Toast.makeText(getActivity().getApplicationContext(), getString(R.string.invalid_player_connection_format), Toast.LENGTH_LONG).show();
    			}
    			else {
    				doConnect(info);
    			}
    		}
    		catch (UnknownHostException e) {
    			Toast.makeText(getActivity().getApplicationContext(), getString(R.string.invalid_player_connection_format), Toast.LENGTH_LONG).show();
    		}
    		catch (IllegalArgumentException e) {
    			Toast.makeText(getActivity().getApplicationContext(), getString(R.string.invalid_player_connection_format), Toast.LENGTH_LONG).show();
    		}
    	}
    }
    
	protected static HashMap<String, ServerInfo> servers = new HashMap<String, ServerInfo>();
	protected static ArrayList<String> serverNames = new ArrayList<String>();
	private boolean connectWithFirstServer = true;

	@Override
	public void serverFound(ServerInfo server) {
		if (!servers.containsKey(server.getName())) {
			Log.d("ConnectedFragment", "New server " + server.getName() + " found");
			servers.put(server.getName(), server);
			serverNames.add(server.getName());
			if (connectWithFirstServer && servers.size() == 1) {
				connectWithFirstServer = false;
				doConnect(server);
			}
			else {
				Log.d("ConnectedFragment", "Not connecting with found server");
			}
		}
	}

	private int getServerSearchPort() {
        String portString = PreferenceManager.getDefaultSharedPreferences(getActivity().getBaseContext()).getString("udp_port", "8009");
        int port = 8009;
        try {
        	port = Integer.parseInt(portString);
        }
        catch (NumberFormatException e) {
        	port = 8009;
        }
        return port;
	}
	
	private static class ConnectionManager {
		
		private static ConnectionManager sInstance;
		
		public static ConnectionManager getInstance() {
			if (sInstance == null)
				sInstance = new ConnectionManager();
			return sInstance;
		}
		
		private int mClients = 0;
		
		public int addClient() {
			Log.d("ConnectedFragment", "Now " + (mClients + 1) + " clients");
			return ++mClients;
		}
		
		public int removeClient() {
			Log.d("ConnectedFragment", "Now " + (mClients - 1) + " clients");
			return --mClients;
		}
	}
}