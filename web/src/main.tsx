import React from "react";
import ReactDOM from "react-dom/client";
import { applyTelegramTheme } from "./telegram/theme";
import App from "./App";
import "./index.css";

applyTelegramTheme();

ReactDOM.createRoot(document.getElementById("root")!).render(
	<React.StrictMode>
		<App />
	</React.StrictMode>,
);
