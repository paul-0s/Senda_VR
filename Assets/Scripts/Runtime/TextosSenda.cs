namespace SendaVR
{
    /// <summary>
    /// Textos de Términos y Condiciones (componente "terminos y condiciones" de Figma).
    /// Puedes editarlos aquí o directamente en el Inspector de "PantallaTerminos".
    /// </summary>
    public static class TextosSenda
    {
        public const string TituloTerminos = "TÉRMINOS Y CONDICIONES DE EXPLORACIÓN – SENDA.VR";

        public const string IntroTerminos =
            "Al ingresar a Senda.VR, aceptas las siguientes condiciones de uso, diseñadas para resguardar " +
            "tu integridad tanto dentro del entorno virtual como en tu posterior experiencia en la montaña.";

        public const string Seccion1 =
            "<b>1. El Umbral Digital (Naturaleza y Alcance de la Simulación)</b>\n" +
            "Senda.VR funciona como un espacio de transición, entrenamiento cognitivo y preparación inmersiva " +
            "previo al contacto con la naturaleza. Los entornos virtuales disponibles —como la representación del " +
            "Parque Natural Aguas de Ramón— son gemelos digitales desarrollados mediante técnicas de reconstrucción " +
            "espacial con el objetivo de reducir la incertidumbre logística, familiarizar al visitante con la " +
            "señalética y educar sobre la geografía local.\n\n" +
            "Sin embargo, el usuario comprende y acepta que esta experiencia es una referencia educativa y no un " +
            "sistema de navegación en tiempo real. El entorno físico de la precordillera es dinámico; factores como " +
            "erosiones recientes en el terreno, desprendimientos de rocas, crecidas de esteros, desniveles exactos y " +
            "condiciones meteorológicas cambian constantemente y pueden diferir de lo representado en la simulación.";

        public const string Seccion2 =
            "<b>2. Seguridad Física y Confort en Realidad Virtual</b>\n" +
            "Tu bienestar físico y sensorial es nuestra prioridad absoluta durante el uso de la plataforma. Antes de " +
            "iniciar cualquier recorrido, es obligatorio delimitar y despejar un área de juego física libre de " +
            "obstáculos, mobiliario, mascotas o desniveles que puedan provocar tropiezos o colisiones mientras llevas " +
            "puesto el visor.\n\n" +
            "Para prevenir la cinetosis (motion sickness), Senda.VR integra distintas configuraciones de accesibilidad " +
            "y locomoción, permitiéndote alternar entre desplazamiento por teletransporte, movimiento continuo y giro " +
            "por ángulos graduales. Si durante la exploración experimentas fatiga visual, pérdida de equilibrio, " +
            "sudoración fría, desorientación o náuseas, detén la experiencia de inmediato, retira el visor y descansa " +
            "en un lugar ventilado hasta recuperar tu estado normal. Se recomienda realizar pausas activas cada 20 o " +
            "30 minutos de uso continuo.";

        public const string Seccion3 =
            "<b>3. Autonomía y Responsabilidad en Terreno Físico</b>\n" +
            "Haber completado una ruta de manera exitosa dentro de Senda.VR no certifica tus capacidades físicas ni " +
            "garantiza tu seguridad al enfrentar el sendero real. La simulación elimina el desgaste cardiovascular, el " +
            "peso del equipamiento y las inclemencias climáticas propias de la montaña.\n\n" +
            "Al trasladar tu experiencia al mundo físico para realizar trekking o senderismo, asumes la total y " +
            "exclusiva responsabilidad por tu planificación y seguridad. Esto incluye evaluar tu condición física " +
            "real, portar hidratación y alimentación adecuada, utilizar calzado e indumentaria técnica acorde al " +
            "clima, calcular los tiempos de retorno considerando las horas de luz solar disponibles, informar tu ruta " +
            "a terceros y acatar en todo momento las normativas, cierres de senderos e instrucciones de los " +
            "administradores y guardaparques del recinto. Senda.VR y su equipo desarrollador se eximen de toda " +
            "responsabilidad ante accidentes, extravíos o incidentes ocurridos en terreno.";

        public const string Seccion4 =
            "<b>4. Ética Ambiental y Respeto por el Entorno Nativo</b>\n" +
            "Este ecosistema digital ha sido construido para fomentar el conocimiento, la valoración y la conservación " +
            "de la biodiversidad y el bosque esclerófilo de nuestra precordillera. Cada especie de flora y fauna " +
            "nativa representada en la simulación cumple un rol ecológico fundamental que debe ser respetado.\n\n" +
            "El compromiso que adquieres al explorar Senda.VR trasciende la pantalla: los hábitos de cuidado, " +
            "observación consciente y respeto aprendidos en el entorno virtual deben replicarse íntegramente al " +
            "visitar los parques reales. Adherimos a los principios de \"No Deje Rastro\": mantente siempre dentro de " +
            "los senderos habilitados, no alimentes ni perturbes a la fauna silvestre, no extraigas flora ni " +
            "elementos naturales, y regresa con todos tus residuos. La tecnología es aquí un puente para proteger lo " +
            "que aún estamos a tiempo de conservar.";

        /// <summary>Las 4 páginas. Se arma con constantes para que Unity siempre use el texto actualizado.</summary>
        public static string[] SeccionesTerminos => new[] { Seccion1, Seccion2, Seccion3, Seccion4 };

        public const string TextoCasilla =
            "He leído, comprendido y acepto los Términos y Condiciones de Exploración.";
    }
}
