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
import Settings from "./screens/Settings";
import Rates from "./screens/Rates";
import {
	IconAdd,
	IconReports,
	IconBudgets,
	IconRates,
	IconSettings,
} from "./components/icons";

const TABS = [
	{ to: "/", label: "Add", Icon: IconAdd, end: true },
	{ to: "/spending", label: "Spending", Icon: IconReports },
	{ to: "/budgets", label: "Budgets", Icon: IconBudgets },
	{ to: "/rates", label: "Rates", Icon: IconRates },
];

const TITLES: Record<string, string> = {
	"/": "Add expense",
	"/spending": "Spending",
	"/budgets": "Budgets",
	"/rates": "Rates & gold",
	"/settings": "Settings",
	"/categories": "Categories",
};

function Layout() {
	const { pathname } = useLocation();
	const navigate = useNavigate();
	const title = TITLES[pathname] ?? "Spending";

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
					{TABS.map(({ to, label, Icon, end }) => (
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
