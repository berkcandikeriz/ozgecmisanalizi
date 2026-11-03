package com.ozgecmisanalizi.app;

import android.app.Activity;
import android.app.AlertDialog;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ProgressBar;
import android.widget.Toast;

import java.net.HttpURLConnection;
import java.net.URL;

/** Sunucu adresini soran ve bağlantıyı deneyen ekran. */
public class SetupActivity extends Activity {

    private EditText input;
    private Button connect;
    private ProgressBar checking;
    private final Handler main = new Handler(Looper.getMainLooper());

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_setup);
        EdgeToEdge.applySystemBarPadding(findViewById(R.id.root));

        input = findViewById(R.id.server);
        connect = findViewById(R.id.connect);
        checking = findViewById(R.id.checking);

        String current = Prefs.getServer(this);
        if (!current.isEmpty()) input.setText(current);

        connect.setOnClickListener(v -> submit());
        input.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_GO) {
                submit();
                return true;
            }
            return false;
        });
    }

    private void submit() {
        final String url = Prefs.normalize(input.getText().toString());
        if (url == null) {
            Toast.makeText(this, R.string.setup_invalid, Toast.LENGTH_LONG).show();
            return;
        }
        input.setText(url);
        setBusy(true);
        new Thread(() -> {
            final String error = probe(url);
            main.post(() -> {
                if (isFinishing()) return;
                setBusy(false);
                if (error == null) {
                    save(url);
                } else {
                    new AlertDialog.Builder(this)
                            .setTitle(R.string.setup_fail_title)
                            .setMessage(getString(R.string.setup_fail_msg, error))
                            .setPositiveButton(R.string.setup_save_anyway, (d, w) -> save(url))
                            .setNegativeButton(R.string.setup_fix, null)
                            .show();
                }
            });
        }, "server-probe").start();
    }

    /** Sunucuya kısa bir istek atar; ulaşılabiliyorsa null, değilse hata açıklaması döner. */
    private static String probe(String url) {
        HttpURLConnection conn = null;
        try {
            conn = (HttpURLConnection) new URL(url + "/").openConnection();
            conn.setConnectTimeout(6000);
            conn.setReadTimeout(10000);
            conn.setInstanceFollowRedirects(true);
            int code = conn.getResponseCode();
            return code >= 500 ? "HTTP " + code : null;
        } catch (Exception e) {
            String msg = e.getMessage();
            return e.getClass().getSimpleName() + (msg == null ? "" : ": " + msg);
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private void save(String url) {
        Prefs.setServer(this, url);
        setResult(RESULT_OK);
        finish();
    }

    private void setBusy(boolean busy) {
        connect.setEnabled(!busy);
        connect.setText(busy ? R.string.setup_checking : R.string.setup_connect);
        checking.setVisibility(busy ? View.VISIBLE : View.GONE);
    }
}
