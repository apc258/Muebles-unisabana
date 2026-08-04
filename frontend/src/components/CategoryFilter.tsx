const categories = [
  { value: 'all', label: 'Todas' },
  { value: 'salas', label: 'Salas' },
  { value: 'comedores', label: 'Comedores' }
];

interface Props {
  selected: string;
  onChange: (category: string) => void;
}

export function CategoryFilter({ selected, onChange }: Props) {
  return (
    <div className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-stone-200">
      <h3 className="mb-4 text-lg font-semibold">Filtros</h3>
      <div className="space-y-2">
        {categories.map((category) => (
          <button
            key={category.value}
            className={`w-full rounded-xl px-4 py-3 text-left ${
              selected === category.value ? 'bg-brand-700 text-white' : 'bg-stone-100 text-stone-700'
            }`}
            onClick={() => onChange(category.value)}
          >
            {category.label}
          </button>
        ))}
      </div>
    </div>
  );
}
