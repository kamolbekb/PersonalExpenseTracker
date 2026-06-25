import {
	createBrowserRouter,
	RouterProvider,
	NavLink,
	Navigate,
	Outlet,
	useLocation,
	useNavigate,
} from "react-router-dom";
import AddExpense from "./screens/AddExpense";
import Categories from "./screens/Categories";
import Budgets from "./screens/Budgets";
import Spending from "./screens/Spending";
import Income from "./screens/Income";
import AddIncome from "./screens/AddIncome";
import Settings from "./screens/Settings";
import Rates from "./screens/Rates";
import { useSettings } from "./api/hooks";
import {
	IconAdd,
	IconReports,
	IconIncome,
	IconBudgets,
	IconRates,
	IconSettings,
} from "./components/icons";

const TITLES: Record<string, string> = {
	"/": "Add expense",
	"/spending": "Spending",
	"/income": "Income",
	"/income/add": "Add income",
	"/budgets": "Budgets",
	"/rates": "Rates & gold",
	"/settings": "Settings",
	"/categories": "Categories",
};

function Layout() {
	const { pathname } = useLocation();
	const navigate = useNavigate();
	const { data: settings } = useSettings();
	const title = TITLES[pathname] ?? "Spending";

	const tabs = [
		{ to: "/", label: "Add", Icon: IconAdd, end: true },
		{ to: "/spending", label: "Spending", Icon: IconReports, end: false },
		...(settings?.incomeTrackingEnabled
			? [{ to: "/income", label: "Income", Icon: IconIncome, end: false }]
			: []),
		{ to: "/budgets", label: "Budgets", Icon: IconBudgets, end: false },
		{ to: "/rates", label: "Rates", Icon: IconRates, end: false },
	];

	return (
		<div className="app">
			<header className="app__header">
				<h1 className="app__title">{title}</h1>
				<button
					className="icon-btn"
					aria-label="Settings"
					onClick={() => navigate("/settings")}
				>
					<IconSettings />
				</button>
			</header>

			<main className="app__main">
				<Outlet />
			</main>

			<nav className="tabbar">
				<div className="tabbar__inner">
					{tabs.map(({ to, label, Icon, end }) => (
						<NavLink key={to} to={to} end={end}>
							<Icon />
							{label}
						</NavLink>
					))}
				</div>
			</nav>
		</div>
	);
}

const router = createBrowserRouter([
	{
		path: "/",
		element: <Layout />,
		children: [
			{ index: true, element: <AddExpense /> },
			{ path: "spending", element: <Spending /> },
			{ path: "income", element: <Income /> },
			{ path: "income/add", element: <AddIncome /> },
			{ path: "expenses", element: <Navigate to="/spending" replace /> },
			{ path: "reports", element: <Navigate to="/spending" replace /> },
			{ path: "categories", element: <Categories /> },
			{ path: "budgets", element: <Budgets /> },
			{ path: "settings", element: <Settings /> },
			{ path: "rates", element: <Rates /> },
		],
	},
]);

export const AppRouter = () => <RouterProvider router={router} />;
