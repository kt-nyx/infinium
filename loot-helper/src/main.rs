use chrono::Utc;
use libloot::metadata::{self, MessageType};
use libloot::{libloot_version, Database, EvalMode, Game, GameType, MergeMode};
use serde::{Deserialize, Serialize};
use std::collections::{HashMap, HashSet};
use std::fs;
use std::io::{self, Read};
use std::path::{Path, PathBuf};
use std::sync::{Arc, RwLock};

#[derive(Debug, Deserialize)]
struct LiblootProfile {
    plugins: Vec<String>,
    // JSON from the Electron side uses camelCase `modRoots`; map it onto a
    // snake_case field for idiomatic Rust usage.
    #[serde(rename = "modRoots")]
    mod_roots: Vec<String>,
}

#[derive(Debug, Deserialize)]
struct LiblootRequest {
    // JSON field is `game` (no rename needed, but we keep the attribute here
    // for clarity alongside `gamePath`).
    #[serde(rename = "game")]
    game: String,
    // JSON from the Electron side uses camelCase `gamePath`; map it onto a
    // snake_case field for idiomatic Rust usage.
    #[serde(rename = "gamePath")]
    game_path: String,
    profile: LiblootProfile,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct LiblootStats {
    plugins_analysed: usize,
    missing_master_count: usize,
    warning_count: usize,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct MissingMaster {
    plugin: String,
    masters: Vec<String>,
}

#[derive(Debug, Serialize, Clone, Copy)]
#[serde(rename_all = "camelCase")]
enum PluginMessageSeverity {
    Error,
    Warning,
    Note,
}

impl From<MessageType> for PluginMessageSeverity {
    fn from(value: MessageType) -> Self {
        match value {
            MessageType::Error => PluginMessageSeverity::Error,
            // `MessageType` variants are `Say`, `Warn`, `Error`:
            // - Treat `Warn` as a warning-level message.
            // - Treat `Say` as an informational/note-level message.
            MessageType::Warn => PluginMessageSeverity::Warning,
            MessageType::Say => PluginMessageSeverity::Note,
        }
    }
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct PluginMessage {
    plugin: String,
    severity: PluginMessageSeverity,
    text: String,
    language: Option<String>,
    condition: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct PluginReport {
    name: String,
    index: usize,
    sorted_index: usize,
    is_active: bool,
    is_master: bool,
    is_light_plugin: bool,
    is_empty: bool,
    loads_archive: bool,
    version: Option<String>,
    bash_tags: Vec<String>,
    missing_masters: Vec<String>,
    requirements: Vec<String>,
    incompatibilities: Vec<String>,
    messages: Vec<PluginMessage>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct LiblootMetadata {
    loot_version: Option<String>,
    game_id: String,
    normalized_game_path: String,
    stats: LiblootStats,
    ambiguous_load_order: Option<bool>,
    plugins: Vec<PluginReport>,
    #[serde(skip_serializing_if = "Option::is_none")]
    extra: Option<serde_json::Value>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct LiblootResponse {
    timestamp: String,
    missing_masters: Vec<MissingMaster>,
    warnings: Vec<String>,
    sorted_load_order: Vec<String>,
    metadata: LiblootMetadata,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<String>,
}

fn map_game_type(game: &str) -> Result<GameType, String> {
    match game {
        "SkyrimSE" | "SkyrimAE" => Ok(GameType::SkyrimSE),
        other => Err(format!(
            "Unsupported game '{other}'. Only Skyrim SE/AE are supported by libloot helper."
        )),
    }
}

fn normalize_game_path(raw_path: &str) -> (PathBuf, PathBuf) {
    let mut game_root = PathBuf::from(raw_path);

    // If the configured path points directly at a Data folder, treat its
    // parent as the game root. Otherwise, assume the given path is the root
    // and append "Data" as the plugins directory.
    let data_dir = if let Some(name) = game_root.file_name().and_then(|n| n.to_str()) {
        if name.eq_ignore_ascii_case("data") {
            if let Some(parent) = game_root.parent() {
                game_root = parent.to_path_buf();
            }
            PathBuf::from(raw_path)
        } else {
            game_root.join("Data")
        }
    } else {
        game_root.join("Data")
    };

    (game_root, data_dir)
}

fn files_to_strings(files: &[metadata::File]) -> Vec<String> {
    files
        .iter()
        .map(|f| f.name().as_str().to_owned())
        .collect()
}

fn build_plugin_reports(
    game: &Game,
    db: &Database,
    sorted_load_order: &[String],
    present_plugins_lower: &HashSet<String>,
) -> (Vec<PluginReport>, Vec<MissingMaster>, usize, Vec<String>) {
    let mut reports = Vec::new();
    let mut missing_master_entries = Vec::new();
    let mut total_missing_masters = 0usize;
    let mut warnings = Vec::new();

    // Map plugin name (lower-cased) -> original index for additional context.
    let mut original_index: HashMap<String, usize> = HashMap::new();
    for (idx, name) in game.load_order().iter().enumerate() {
        original_index.insert((*name).to_lowercase(), idx);
    }

    let locale = std::env::var("LOOT_HELPER_LOCALE").unwrap_or_else(|_| "en".to_string());

    for (sorted_index, plugin_name) in sorted_load_order.iter().enumerate() {
        let plugin_lower = plugin_name.to_lowercase();
        let plugin_arc: Option<Arc<libloot::Plugin>> = game.plugin(plugin_name);
        let Some(plugin_obj) = plugin_arc else {
            continue;
        };

        // Missing masters based purely on plugin headers and the set of
        // currently-present plugins.
        let mut missing_for_plugin = Vec::new();
        if let Ok(masters) = plugin_obj.masters() {
            for master in masters {
                if !present_plugins_lower.contains(&master.to_lowercase()) {
                    missing_for_plugin.push(master);
                }
            }
        }

        if !missing_for_plugin.is_empty() {
            total_missing_masters += missing_for_plugin.len();
            missing_master_entries.push(MissingMaster {
                plugin: plugin_name.clone(),
                masters: missing_for_plugin.clone(),
            });
        }

        // Metadata (messages, requirements, incompatibilities, bash tags).
        let mut messages_out = Vec::new();
        let mut requirements = Vec::new();
        let mut incompatibilities = Vec::new();
        let mut bash_tags = Vec::new();

        if let Ok(meta_opt) = db.plugin_metadata(
            plugin_name,
            MergeMode::WithUserMetadata,
            EvalMode::Evaluate,
        ) {
            if let Some(meta) = meta_opt {
                requirements.extend(files_to_strings(meta.requirements()));
                incompatibilities.extend(files_to_strings(meta.incompatibilities()));
                bash_tags.extend(meta.tags().iter().map(|tag| tag.name().to_owned()));

                for msg in meta.messages() {
                    if let Some(content) =
                        metadata::select_message_content(msg.content(), &locale)
                    {
                        let severity: PluginMessageSeverity = msg.message_type().into();
                        let text = content.text().to_owned();
                        let condition = msg.condition().map(|s| s.to_owned());
                        let language = Some(content.language().to_owned());

                if matches!(severity, PluginMessageSeverity::Error | PluginMessageSeverity::Warning)
                {
                    warnings.push(format!("{plugin_name}: {text}"));
                }

                        messages_out.push(PluginMessage {
                            plugin: plugin_name.clone(),
                            severity,
                            text,
                            language,
                            condition,
                        });
                    }
                }
            }
        }

        let version = plugin_obj.version().map(|v| v.to_owned());

        let report = PluginReport {
            name: plugin_name.clone(),
            index: *original_index.get(&plugin_lower).unwrap_or(&0),
            sorted_index,
            is_active: game.is_plugin_active(plugin_name),
            is_master: plugin_obj.is_master(),
            is_light_plugin: plugin_obj.is_light_plugin(),
            is_empty: plugin_obj.is_empty(),
            loads_archive: plugin_obj.loads_archive(),
            version,
            bash_tags,
            missing_masters: missing_for_plugin,
            requirements,
            incompatibilities,
            messages: messages_out,
        };

        reports.push(report);
    }

    (
        reports,
        missing_master_entries,
        total_missing_masters,
        warnings,
    )
}

fn read_request() -> Result<LiblootRequest, String> {
    let mut buf = String::new();
    io::stdin()
        .read_to_string(&mut buf)
        .map_err(|err| format!("Failed to read request from stdin: {err}"))?;

    serde_json::from_str(&buf).map_err(|err| format!("Failed to parse request JSON: {err}"))
}

fn analyse_with_libloot(request: LiblootRequest) -> Result<LiblootResponse, String> {
    let game_type = map_game_type(&request.game)?;
    let (game_root, data_dir) = normalize_game_path(&request.game_path);

    let mut game = Game::new(game_type, &game_root)
        .map_err(|err| format!("Failed to create libloot Game: {err}"))?;

    // Ensure the main data directory always exists in the additional paths
    // list, then layer MO2 mod roots on top so they take precedence.
    let mut additional_paths: Vec<PathBuf> = Vec::new();
    additional_paths.push(data_dir.clone());
    for mod_root in &request.profile.mod_roots {
        additional_paths.push(PathBuf::from(mod_root));
    }

    game.set_additional_data_paths(additional_paths)
        .map_err(|err| format!("Failed to set additional data paths: {err}"))?;

    // Resolve the profile's plugin names into actual file paths under the
    // base Data directory or one of the MO2 mod roots. libloot validates
    // that all plugin paths are sane and within the game data tree; if we
    // pass arbitrary or non-existent paths it will fail with
    // "failed validation of input plugin paths".
    fn resolve_plugin_paths(
        data_dir: &Path,
        mod_roots: &[String],
        plugin_names: &[String],
    ) -> (Vec<PathBuf>, Vec<String>) {
        let mut resolved_paths = Vec::new();
        let mut resolved_names = Vec::new();
        let mut missing = Vec::new();

        let mod_root_paths: Vec<PathBuf> = mod_roots.iter().map(PathBuf::from).collect();

        for name in plugin_names {
            // First, try the main Data directory.
            let mut found: Option<PathBuf> = {
                let candidate = data_dir.join(name);
                if fs::metadata(&candidate).map(|m| m.is_file()).unwrap_or(false) {
                    Some(candidate)
                } else {
                    None
                }
            };

            // If not found in Data, walk the MO2 mod roots (which act as
            // additional VFS "Data" roots).
            if found.is_none() {
                for root in &mod_root_paths {
                    let candidate = root.join(name);
                    if fs::metadata(&candidate).map(|m| m.is_file()).unwrap_or(false) {
                        found = Some(candidate);
                        break;
                    }
                }
            }

            if let Some(path) = found {
                resolved_names.push(name.clone());
                resolved_paths.push(path);
            } else {
                missing.push(name.clone());
            }
        }

        // For now we only return the successfully-resolved names & paths;
        // the list of missing names is for potential future diagnostics.
        (resolved_paths, missing)
    }

    let (plugin_path_bufs, _missing_plugin_names) = resolve_plugin_paths(
        &data_dir,
        &request.profile.mod_roots,
        &request.profile.plugins,
    );

    if plugin_path_bufs.is_empty() {
        return Err("No valid plugin files found under the configured game Data path and MO2 mod roots.".to_string());
    }

    let plugin_paths: Vec<&Path> = plugin_path_bufs.iter().map(|p| p.as_path()).collect();

    game.load_plugins(&plugin_paths)
        .map_err(|err| format!("Failed to load plugins: {err}"))?;

    // Use the successfully-resolved plugin names when asking libloot to
    // calculate a sorted load order, so we don't reference plugins that
    // couldn't be mapped to real files.
    let plugin_names: Vec<&str> = plugin_path_bufs
        .iter()
        .filter_map(|p| p.file_name().and_then(|n| n.to_str()))
        .collect();

    let sorted_load_order = game
        .sort_plugins(&plugin_names)
        .map_err(|err| format!("Failed to sort plugins: {err}"))?;

    // Build helper structures for analysis.
    let present_plugins_lower: HashSet<String> = request
        .profile
        .plugins
        .iter()
        .map(|p| p.to_lowercase())
        .collect();

    let db_arc: Arc<RwLock<Database>> = game.database();
    let db_guard = db_arc
        .write()
        .map_err(|_| "Failed to acquire write lock on libloot Database".to_string())?;
    let db_ref: &Database = &*db_guard;

    let (plugin_reports, missing_master_entries, total_missing_masters, mut warnings) =
        build_plugin_reports(&game, db_ref, &sorted_load_order, &present_plugins_lower);

    let ambiguous_load_order = game
        .is_load_order_ambiguous()
        .map(Some)
        .unwrap_or(None);

    if let Some(true) = ambiguous_load_order {
        warnings.push(
            "LOOT reported an ambiguous load order; review plugin positions carefully."
                .to_string(),
        );
    }

    let loot_version = Some(libloot_version().to_owned());

    let stats = LiblootStats {
        plugins_analysed: request.profile.plugins.len(),
        missing_master_count: total_missing_masters,
        warning_count: warnings.len(),
    };

    let metadata = LiblootMetadata {
        loot_version,
        game_id: request.game,
        normalized_game_path: game_root.to_string_lossy().to_string(),
        stats,
        ambiguous_load_order,
        plugins: plugin_reports,
        extra: None,
    };

    Ok(LiblootResponse {
        timestamp: Utc::now().to_rfc3339(),
        missing_masters: missing_master_entries,
        warnings,
        sorted_load_order,
        metadata,
        error: None,
    })
}

fn main() {
    let request = match read_request() {
        Ok(req) => req,
        Err(message) => {
            eprintln!("{message}");
            let warnings = vec![format!("libloot helper error: {message}")];
            let error_response = LiblootResponse {
                timestamp: Utc::now().to_rfc3339(),
                missing_masters: Vec::new(),
                warnings: warnings.clone(),
                sorted_load_order: Vec::new(),
                metadata: LiblootMetadata {
                    loot_version: None,
                    game_id: "unknown".to_string(),
                    normalized_game_path: String::new(),
                    stats: LiblootStats {
                        plugins_analysed: 0,
                        missing_master_count: 0,
                        warning_count: warnings.len(),
                    },
                    ambiguous_load_order: None,
                    plugins: Vec::new(),
                    extra: None,
                },
                error: Some(message),
            };

            if let Ok(json) = serde_json::to_string(&error_response) {
                println!("{json}");
            }
            std::process::exit(1);
        }
    };

    let response = match analyse_with_libloot(request) {
        Ok(resp) => resp,
        Err(message) => {
            eprintln!("{message}");
            let warnings = vec![format!("libloot helper error: {message}")];
            let error_response = LiblootResponse {
                timestamp: Utc::now().to_rfc3339(),
                missing_masters: Vec::new(),
                warnings: warnings.clone(),
                sorted_load_order: Vec::new(),
                metadata: LiblootMetadata {
                    loot_version: None,
                    game_id: "unknown".to_string(),
                    normalized_game_path: String::new(),
                    stats: LiblootStats {
                        plugins_analysed: 0,
                        missing_master_count: 0,
                        warning_count: warnings.len(),
                    },
                    ambiguous_load_order: None,
                    plugins: Vec::new(),
                    extra: None,
                },
                error: Some(message),
            };

            match serde_json::to_string(&error_response) {
                Ok(json) => {
                    println!("{json}");
                }
                Err(err) => {
                    eprintln!("Failed to serialise error response JSON: {err}");
                }
            }
            std::process::exit(1);
        }
    };

    match serde_json::to_string(&response) {
        Ok(json) => {
            println!("{json}");
        }
        Err(err) => {
            eprintln!("Failed to serialise response JSON: {err}");
            std::process::exit(1);
        }
    }
}


