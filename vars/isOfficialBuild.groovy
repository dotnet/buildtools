/* return true if officialBuildId is valid
 */
def call() {
    if(env.OfficialBuildId != null && 
       env.OfficialBuildId != '' && 
       env.OfficialBuildId != 'none') {
        return true
    }
    return false
}