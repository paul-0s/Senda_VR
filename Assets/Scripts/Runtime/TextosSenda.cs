namespace SendaVR
{
    /// <summary>
    /// Textos de Términos y Condiciones (tomados del "Panel de normativa" de Figma).
    /// Puedes editarlos aquí o directamente en el Inspector de "PantallaTerminos".
    /// </summary>
    public static class TextosSenda
    {
        public const string TituloTerminos = "TÉRMINOS Y CONDICIONES DE EXPLORACIÓN – SENDA.VR";

        public const string IntroTerminos =
            "Al ingresar a Senda.VR, aceptas los siguientes términos de uso, diseñados para resguardar " +
            "tu integridad tanto dentro del entorno virtual como en tu posterior experiencia en la montaña.";

        public static readonly string[] SeccionesTerminos =
        {
            "<b>1. El Umbral Digital (Naturaleza de la Simulación)</b>\n" +
            "Senda.VR es un espacio de transición y preparación inmersiva. Los entornos generados, como el " +
            "Parque Aguas de Ramón, son gemelos digitales diseñados para reducir la incertidumbre logística y " +
            "educar sobre el entorno. Ten en cuenta que las condiciones físicas, desniveles exactos y el clima " +
            "en el mundo real siempre pueden variar.",

            "<b>2. Seguridad y Confort en Realidad Virtual</b>\n" +
            "Tu bienestar es prioridad. Asegúrate de configurar un área de juego física despejada antes de " +
            "iniciar. Senda.VR incluye múltiples opciones de locomoción (teletransporte o movimiento continuo). " +
            "Si experimentas fatiga visual, desorientación o mareos (motion sickness), pausa la simulación, " +
            "retira el visor y descansa de inmediato.",

            "<b>3. Responsabilidad en Terreno Físico</b>\n" +
            "Explorar una ruta en nuestra plataforma no garantiza tu seguridad en el cerro. Al realizar trekking " +
            "en el mundo físico, es tu exclusiva responsabilidad llevar hidratación suficiente, indumentaria " +
            "técnica, planificar tus tiempos de luz solar y acatar las instrucciones de los administradores y " +
            "guardaparques.",

            "<b>4. Respeto por el Entorno Nativo</b>\n" +
            "Este mundo fue creado para que conozcas y valores nuestra flora y fauna endémica. El cuidado y " +
            "respeto que aprendas en este entorno virtual es el mismo que deberás aplicar en la naturaleza. " +
            "No dejes rastro, ni digital ni físico."
        };

        public const string TextoCasilla =
            "He leído, comprendido y acepto los Términos y Condiciones de Exploración.";
    }
}
